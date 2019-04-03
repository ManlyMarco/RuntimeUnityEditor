using System;
using System.Collections.Generic;
using System.Collections;
using System.Linq;
using System.Reflection;
using System.Text;
using Mono.CSharp;
using RuntimeUnityEditor.Inspector.Entries;
using UnityEngine;
using Attribute = System.Attribute;
using Object = UnityEngine.Object;

namespace RuntimeUnityEditor.REPL
{
    public class REPL : InteractiveBase
    {
        private static readonly GameObject go;

        static REPL()
        {
            go = new GameObject("UnityREPL");
            go.transform.parent = BepInEx.Bootstrap.Chainloader.ManagerObject.transform;
            MB = go.AddComponent<ReplHelper>();
        }

        public static new string help
        {
            get
            {
                string original = InteractiveBase.help;

                var sb = new StringBuilder();
                sb.AppendLine("In addition, the following helper methods are provided:");
                foreach (var methodInfo in typeof(REPL).GetMethods(BindingFlags.Public | BindingFlags.Static))
                {
                    var attr = methodInfo.GetCustomAttributes(typeof(DocumentationAttribute), false);
                    if (attr.Length == 0)
                        continue;
                    sb.Append("  ");
                    sb.AppendLine(((DocumentationAttribute)attr[0]).Docs);
                }

                return $"{original}\n{sb}";
            }
        }


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
            foreach (var component in Object.FindObjectsOfType<Component>())
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
            return RuntimeUnityEditor.Instance.Inspector.GetInspectedObject()
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
            RuntimeUnityEditor.Instance.Inspector.InspectorPush(new InstanceStackEntry(obj, "REPL > " + obj.GetType().Name));
        }

        [Documentation("setis(type) - send the static class to the inspector.")]
        public static void setis(Type type)
        {
            if (type == null) throw new ArgumentNullException(nameof(type));
            RuntimeUnityEditor.Instance.Inspector.InspectorPush(new StaticStackEntry(type, "REPL > " + type.Name));
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