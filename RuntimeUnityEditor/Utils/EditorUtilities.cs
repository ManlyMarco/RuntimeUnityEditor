using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using BepInEx.Logging;
using RuntimeUnityEditor.Inspector.Entries;
using UnityEngine;
using Logger = BepInEx.Logger;
using Object = UnityEngine.Object;

namespace RuntimeUnityEditor.Utils
{
    public static class EditorUtilities
    {
        private static Texture2D WindowBackground { get; set; }

        public static void DrawSolidWindowBackground(Rect windowRect)
        {
            if (WindowBackground == null)
            {
                var windowBackground = new Texture2D(1, 1, TextureFormat.ARGB32, false);
                windowBackground.SetPixel(0, 0, new Color(0.6f, 0.6f, 0.6f, 1));
                windowBackground.Apply();
                WindowBackground = windowBackground;
            }

            // It's necessary to make a new GUIStyle here or the texture doesn't show up
            GUI.Box(windowRect, GUIContent.none, new GUIStyle { normal = new GUIStyleState { background = WindowBackground } });
        }

        public static void DrawSeparator()
        {
            GUILayout.Space(5);
        }

        private static readonly Dictionary<Type, Func<object, string>> CustomObjectToString = new Dictionary<Type, Func<object, string>>();

        public static void AddCustomObjectToString<TObj>(Func<TObj, string> func)
        {
            CustomObjectToString.Add(typeof(TObj), o => func.Invoke((TObj)o));
        }

        public static string ExtractText(object value)
        {
            if (value == null) return "NULL";
            switch (value)
            {
                case string str:
                    return str;
                case Transform t:
                    return t.name;
                case GameObject o:
                    return o.name;
                case Exception ex:
                    return "EXCEPTION: " + ex.Message;
            }

            var valueType = value.GetType();

            if (CustomObjectToString.TryGetValue(valueType, out var func))
                return func(value);

            if(value is ICollection collection)
                return $"Count = {collection.Count}";

            if (value is IEnumerable _)
            {
                var property = valueType.GetProperty("Count", BindingFlags.Public | BindingFlags.Instance);
                if (property != null && property.CanRead)
                {
                    if (property.GetValue(value, null) is int count)
                        return $"Count = {count}";
                }
                
                return "IS ENUMERABLE";
            }

            try
            {
                if (valueType.IsGenericType)
                {
                    var baseType = valueType.GetGenericTypeDefinition();
                    if (baseType == typeof(KeyValuePair<,>))
                    {
                        //var argTypes = baseType.GetGenericArguments();
                        var kvpKey = valueType.GetProperty("Key")?.GetValue(value, null);
                        var kvpValue = valueType.GetProperty("Value")?.GetValue(value, null);
                        return $"[{ExtractText(kvpKey)} | {ExtractText(kvpValue)}]";
                    }
                }

                return value.ToString();
            }
            catch
            {
                return valueType.Name;
            }
        }

        public static IEnumerable<ReadonlyCacheEntry> GetTransformScanner()
        {
            Logger.Log(LogLevel.Debug, "[CheatTools] Looking for Transforms...");

            var trt = typeof(Transform);
            var types = GetAllComponentTypes().Where(x => trt.IsAssignableFrom(x));

            foreach (var component in ScanComponentTypes(types, false))
                yield return component;
        }

        public static IEnumerable<ReadonlyCacheEntry> GetRootGoScanner()
        {
            Logger.Log(LogLevel.Debug, "[CheatTools] Looking for Root Game Objects...");

            foreach (GameObject go in Resources.FindObjectsOfTypeAll<GameObject>().Where(x => x.transform == x.transform.root))
                yield return new ReadonlyCacheEntry($"GameObject({go.name})", go);
        }

        public static IEnumerable<ReadonlyCacheEntry> GetMonoBehaviourScanner()
        {
            Logger.Log(LogLevel.Debug, "[CheatTools] Looking for MonoBehaviours...");

            var mbt = typeof(MonoBehaviour);
            var types = GetAllComponentTypes().Where(x => mbt.IsAssignableFrom(x));

            foreach (var component in ScanComponentTypes(types, true))
                yield return component;
        }

        public static IEnumerable<ReadonlyCacheEntry> GetComponentScanner()
        {
            Logger.Log(LogLevel.Debug, "[CheatTools] Looking for Components...");

            var mbt = typeof(MonoBehaviour);
            var trt = typeof(Transform);
            var allComps = GetAllComponentTypes().ToList();
            var types = allComps.Where(x => !mbt.IsAssignableFrom(x) && !trt.IsAssignableFrom(x));

            foreach (var component in ScanComponentTypes(types, true))
                yield return component;
        }

        private static IEnumerable<ReadonlyCacheEntry> ScanComponentTypes(IEnumerable<Type> types, bool noTransfroms)
        {
            var allObjects = from type in types
                             let components = Object.FindObjectsOfType(type).OfType<Component>()
                             from component in components
                             where !(noTransfroms && component is Transform)
                             select component;

            string GetTransformPath(Transform tr)
            {
                if (tr.parent != null)
                    return GetTransformPath(tr.parent) + "/" + tr.name;
                return tr.name;
            }

            foreach (var obj in allObjects.Distinct())
                yield return new ReadonlyCacheEntry($"{GetTransformPath(obj.transform)} ({obj.GetType().Name})", obj);
        }

        private static IEnumerable<Type> GetAllComponentTypes()
        {
            var compType = typeof(Component);
            return AppDomain.CurrentDomain.GetAssemblies()
                .SelectMany(x =>
                {
                    try
                    {
                        return x.GetTypes();
                    }
                    catch (SystemException)
                    {
                        return Enumerable.Empty<Type>();
                    }
                })
                .Where(t => t.IsClass && !t.IsAbstract && !t.ContainsGenericParameters)
                .Where(compType.IsAssignableFrom);
        }

        public static IEnumerable<ReadonlyCacheEntry> GetInstanceClassScanner()
        {
            Logger.Log(LogLevel.Debug, "[CheatTools] Looking for class instances...");

            var query = AppDomain.CurrentDomain.GetAssemblies()
                .SelectMany(x =>
                {
                    try
                    {
                        return x.GetTypes();
                    }
                    catch (SystemException)
                    {
                        return Enumerable.Empty<Type>();
                    }
                })
                .Where(t => t.IsClass && !t.IsAbstract && !t.ContainsGenericParameters);

            foreach (var type in query)
            {
                object obj = null;
                try
                {
                    obj = type.GetProperty("Instance",
                            BindingFlags.Public | BindingFlags.Static | BindingFlags.FlattenHierarchy)
                        ?.GetValue(null, null);
                }
                catch (Exception ex)
                {
                    Logger.Log(LogLevel.Debug, ex.ToString());
                }
                if (obj != null)
                    yield return new ReadonlyCacheEntry(type.Name + ".Instance", obj);
            }
        }
    }
}