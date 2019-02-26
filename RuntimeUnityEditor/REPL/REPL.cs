using System;
using System.Collections;
using System.Reflection;
using System.Text;
using Mono.CSharp;
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

        public new static string help
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

        [Documentation("geti() - get object currently opened in inspector. Will get expanded upon accepting. Use like this: var x = geti()")]
        public static object geti()
        {
            return RuntimeUnityEditor.Instance.Inspector.GetInspectedObject();
        }

        //[Documentation("geti<T>() - get object currently opened in inspector. Use geti() instead.")]
        public static T geti<T>()
        {
            return (T) RuntimeUnityEditor.Instance.Inspector.GetInspectedObject();
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