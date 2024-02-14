using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Text;
using BepInEx.Unity.IL2CPP;
using Mono.CSharp;
using RuntimeUnityEditor.Core.Inspector.Entries;
using RuntimeUnityEditor.Core.ObjectTree;
using RuntimeUnityEditor.Core.Utils.Abstractions;
using RuntimeUnityEditor.Core.Utils.ObjectDumper;
using UnityEngine;
using Attribute = System.Attribute;
using Object = UnityEngine.Object;
#pragma warning disable CS1591

namespace RuntimeUnityEditor.Core.REPL
{
    /// <summary>
    /// C# REPL environment. Everything in here can be called directly in the REPL console.
    /// </summary>
    public class REPL : InteractiveBase
    {
        static REPL()
        {
            //var go = new GameObject("UnityREPL");
            //go.transform.parent = RuntimeUnityEditorCore.PluginObject.transform;
            //MB = go.AddComponent<ReplHelper>();
            MB = IL2CPPChainloader.AddUnityComponent<ReplHelper>();
        }

        public static string clear
        {
            get
            {
                ReplWindow.Instance.Clear();
                return "Log cleared";
            }
        }

        public static new string help
        {
            get
            {
                string original = InteractiveBase.help;

                var sb = new StringBuilder();
                sb.AppendLine("  clear;                   - Clear this log\n");
                sb.AppendLine();
                sb.AppendLine("In addition, the following helper methods are provided:");
                foreach (var methodInfo in typeof(REPL).GetMethods(BindingFlags.Public | BindingFlags.Static))
                {
                    var attr = methodInfo.GetCustomAttributes(typeof(DocumentationAttribute), false);
                    if (attr.Length == 0)
                        continue;
                    sb.Append("  ");
                    sb.AppendLine(((DocumentationAttribute)attr[0]).Docs);
                }

                return $"{original}{sb}";
            }
        }

        public static object InteropTempVar { get; set; }

        [Documentation("MB - A dummy MonoBehaviour for accessing Unity.")]
        public static ReplHelper MB { get; }

        [Documentation("find<T>() - find a UnityEngine.Object of type T.")]
        public static T find<T>() where T : Object
        {
            return MB.Find<T>();
        }

        [Documentation("findAll<T>() - find all UnityEngine.Object of type T.")]
        public static T[] findAll<T>() where T : Object
        {
            return MB.FindAll<T>();
        }

        [Documentation("runCoroutine(enumerator) - runs an IEnumerator as a Unity coroutine.")]
        public static Coroutine runCoroutine(IEnumerator i)
        {
            return MB.RunCoroutine(i);
        }

        [Documentation("endCoroutine(co) - ends a Unity coroutine.")]
        public static void endCoroutine(Coroutine c)
        {
            MB.EndCoroutine(c);
        }

        [Documentation("type<T>() - obtain type info about a type T. Provides some Reflection helpers.")]
        public static TypeHelper type<T>()
        {
            return new TypeHelper(typeof(T));
        }

        [Documentation("type(obj) - obtain type info about object obj. Provides some Reflection helpers.")]
        public static TypeHelper type(object instance)
        {
            return new TypeHelper(instance);
        }

        [Documentation("dir(obj) - lists all available methods and fiels of a given obj.")]
        public static string dir(object instance)
        {
            return type(instance).info();
        }

        [Documentation("dir<T>() - lists all available methods and fields of type T.")]
        public static string dir<T>()
        {
            return type<T>().info();
        }

        [Documentation("findrefs(obj) - find references to the object in currently loaded components.")]
        public static Component[] findrefs(object obj)
        {
            if (obj == null) throw new ArgumentNullException(nameof(obj));

            var results = new List<Component>();
            foreach (var component in Object.FindObjectsOfType<Component>().Select(c => c.TryAutoCast() as Component ?? c))
            {
                var type = component.GetType();

                var nameBlacklist = new[] { "parent", "parentInternal", "root", "transform", "gameObject" };
                var typeBlacklist = new[] { typeof(bool) };

                foreach (var prop in type.GetProperties(BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic)
                    .Where(x => x.CanRead && !nameBlacklist.Contains(x.Name) && !typeBlacklist.Contains(x.PropertyType)))
                {
                    try
                    {
                        if (Equals(prop.GetValue(component, null), obj))
                        {
                            results.Add(component);
                            goto finish;
                        }
                    }
                    catch { }
                }
                foreach (var field in type.GetFields(BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic)
                    .Where(x => !nameBlacklist.Contains(x.Name) && !typeBlacklist.Contains(x.FieldType)))
                {
                    try
                    {
                        if (Equals(field.GetValue(component), obj))
                        {
                            results.Add(component);
                            goto finish;
                        }
                    }
                    catch { }
                }
            finish:;
            }

            return results.ToArray();
        }

        [Documentation("geti() - get object currently opened in inspector. Will get expanded upon accepting. Best to use like this: var x = geti()")]
        public static object geti()
        {
            return Inspector.Inspector.Instance.GetInspectedObject()
                ?? throw new InvalidOperationException("No object is opened in inspector or a static type is opened");
        }

        //[Documentation("geti<T>() - get object currently opened in inspector. Use geti() instead.")]
        public static T geti<T>()
        {
            return (T)geti();
        }

        [Documentation("seti(obj) - send the object to the inspector. To send a static class use the setis(type) command.")]
        public static void seti(object obj)
        {
            if (obj == null) throw new ArgumentNullException(nameof(obj));
            Inspector.Inspector.Instance.Push(new InstanceStackEntry(obj, "REPL > " + obj.GetType().Name), true);
        }

        [Documentation("setis(type) - send the static class to the inspector.")]
        public static void setis(Type type)
        {
            if (type == null) throw new ArgumentNullException(nameof(type));
            Inspector.Inspector.Instance.Push(new StaticStackEntry(type, "REPL > " + type.Name), true);
        }

        [Documentation("getTree() - get the transform currently selected in tree view.")]
        public static Transform getTree()
        {
            return ObjectTreeViewer.Instance.SelectedTransform;
        }

        [Documentation("findTree(Transform) - find and select the transform in the tree view.")]
        public static void findTree(Transform tr)
        {
            if (tr == null) throw new ArgumentNullException(nameof(tr));
            ObjectTreeViewer.Instance.SelectAndShowObject(tr);
        }

        [Documentation("findTree(GameObject) - find and select the object in the tree view.")]
        public static void findTree(GameObject go)
        {
            if (go == null) throw new ArgumentNullException(nameof(go));
            ObjectTreeViewer.Instance.SelectAndShowObject(go.transform);
        }

        [Documentation("dnspy(type) - open the type in dnSpy if dnSpy path is configured.")]
        public static void dnspy(Type type)
        {
            if (type == null) throw new ArgumentNullException(nameof(type));
            DnSpyHelper.OpenInDnSpy(type);
        }

        [Documentation("dnspy(memberInfo) - open the type member in dnSpy if dnSpy is configured.")]
        public static void dnspy(MemberInfo member)
        {
            if (member == null) throw new ArgumentNullException(nameof(member));
            DnSpyHelper.OpenInDnSpy(member);
        }

        [Documentation("echo(string) - write a string to REPL output.")]
        public static void echo(string message)
        {
            ReplWindow.Instance.AppendLogLine(message);
        }

        [Documentation("message(string) - write a string to log.")]
        public static void message(string message)
        {
            RuntimeUnityEditorCore.Logger.Log(LogLevel.Message, message);
        }

        [Documentation("paste(index) - paste clipboard contents from a given int index.")]
        public static object paste(int index)
        {
            return Clipboard.ClipboardWindow.Contents[index];
        }

        [Documentation("copy(object) - copy given object to clipboard (classes are copied by reference, returns the index it was added under).")]
        public static int copy(object @object)
        {
            Clipboard.ClipboardWindow.Contents.Add(@object);
            return Clipboard.ClipboardWindow.Contents.Count - 1;
        }

        [Documentation("dump(object, fileName) - dump given object to a new text file at specitied path.")]
        public static void dump(object @object, string fileName)
        {
            @object.DumpToFile("REPL_OBJECT", fileName);
        }

        [Documentation("dump(object) - dump given object to a temporary text file and open it in notepad. Returns path to the temp file.")]
        public static string dump(object @object)
        {
            return Dumper.DumpToTempFile(@object, "REPL_OBJECT");
        }

        [AttributeUsage(AttributeTargets.Method | AttributeTargets.Property)]
        private class DocumentationAttribute : Attribute
        {
            public DocumentationAttribute(string doc)
            {
                Docs = doc;
            }

            public string Docs { get; }
        }
    }
}