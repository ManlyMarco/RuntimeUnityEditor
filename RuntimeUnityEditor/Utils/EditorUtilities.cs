using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using RuntimeUnityEditor.Core.Inspector.Entries;
using UnityEngine;
using Object = UnityEngine.Object;

namespace RuntimeUnityEditor.Core.Utils
{
    public static class EditorUtilities
    {
        public static void EatInputInRect(Rect eatRect)
        {
            if (eatRect.Contains(new Vector2(Input.mousePosition.x, Screen.height - Input.mousePosition.y)))
                Input.ResetInputAxes();
        }

        public static GUISkin CreateSkin()
        {
            var newSkin = Object.Instantiate(GUI.skin);

            // Load the custom skin from resources
            var texData = ResourceUtils.GetEmbeddedResource("guisharp-box.png");
            var boxTex = UnityFeatureHelper.LoadTexture(texData);
            newSkin.box.onNormal.background = null;
            newSkin.box.normal.background = boxTex;
            newSkin.box.normal.textColor = Color.white;

            texData = ResourceUtils.GetEmbeddedResource("guisharp-window.png");
            var winTex = UnityFeatureHelper.LoadTexture(texData);
            newSkin.window.onNormal.background = null;
            newSkin.window.normal.background = winTex;
            newSkin.window.padding = new RectOffset(6, 6, 22, 6);
            newSkin.window.border = new RectOffset(10, 10, 20, 10);
            newSkin.window.normal.textColor = Color.white;

            newSkin.button.padding = new RectOffset(4, 4, 3, 3);
            newSkin.button.normal.textColor = Color.white;

            newSkin.textField.normal.textColor = Color.white;

            newSkin.label.normal.textColor = Color.white;

            return newSkin;
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

            if (value is ICollection collection)
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
            RuntimeUnityEditorCore.Logger.Log(LogLevel.Debug, "[CheatTools] Looking for Transforms...");

            var trt = typeof(Transform);
            var types = GetAllComponentTypes().Where(x => trt.IsAssignableFrom(x));

            foreach (var component in ScanComponentTypes(types, false))
                yield return component;
        }

        public static IEnumerable<ReadonlyCacheEntry> GetRootGoScanner()
        {
            RuntimeUnityEditorCore.Logger.Log(LogLevel.Debug, "[CheatTools] Looking for Root Game Objects...");

            foreach (GameObject go in Resources.FindObjectsOfTypeAll<GameObject>().Where(x => x.transform == x.transform.root))
                yield return new ReadonlyCacheEntry($"GameObject({go.name})", go);
        }

        public static IEnumerable<ReadonlyCacheEntry> GetMonoBehaviourScanner()
        {
            RuntimeUnityEditorCore.Logger.Log(LogLevel.Debug, "[CheatTools] Looking for MonoBehaviours...");

            var mbt = typeof(MonoBehaviour);
            var types = GetAllComponentTypes().Where(x => mbt.IsAssignableFrom(x));

            foreach (var component in ScanComponentTypes(types, true))
                yield return component;
        }

        public static IEnumerable<ReadonlyCacheEntry> GetComponentScanner()
        {
            RuntimeUnityEditorCore.Logger.Log(LogLevel.Debug, "[CheatTools] Looking for Components...");

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
                .SelectMany(Extensions.GetTypesSafe)
                .Where(t => t.IsClass && !t.IsAbstract && !t.ContainsGenericParameters)
                .Where(compType.IsAssignableFrom);
        }

        public static IEnumerable<ReadonlyCacheEntry> GetInstanceClassScanner()
        {
            RuntimeUnityEditorCore.Logger.Log(LogLevel.Debug, "[CheatTools] Looking for class instances...");

            var query = AppDomain.CurrentDomain.GetAssemblies()
                .SelectMany(Extensions.GetTypesSafe)
                .Where(t => t.IsClass && !t.IsAbstract && !t.ContainsGenericParameters);

            foreach (var type in query)
            {
                object obj = null;
                try
                {
                    obj = type.GetProperty("Instance", BindingFlags.Public | BindingFlags.Static | BindingFlags.FlattenHierarchy)?.GetValue(null, null);
                }
                catch (Exception ex)
                {
                    RuntimeUnityEditorCore.Logger.Log(LogLevel.Debug, ex.ToString());
                }
                if (obj != null)
                    yield return new ReadonlyCacheEntry(type.Name + ".Instance", obj);
            }
        }
    }
}