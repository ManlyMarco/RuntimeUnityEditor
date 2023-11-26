using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using HarmonyLib;
using Il2CppInterop.Runtime.InteropTypes;
using RuntimeUnityEditor.Core.Utils;
using RuntimeUnityEditor.Core.Utils.Abstractions;
using UnityEngine;

namespace RuntimeUnityEditor.Core
{
    internal static class IL2CppExtensions
    {
        public static void Set(this RectOffset obj, int left, int right, int top, int bottom)
        {
            obj.left = left;
            obj.right = right;
            obj.top = top;
            obj.bottom = bottom;
        }
        
        public static Transform[] GetChildren(this Transform transform)
        {
            var children = new Transform[transform.childCount];
            for (var i = 0; i < transform.childCount; i++)
            {
                var child = transform.GetChild(i);
                children[i] = child;
            }
            return children;
        }
        public static Transform[] GetChildrenCasted(this Transform transform)
        {
            var children = new Transform[transform.childCount];
            for (var i = 0; i < transform.childCount; i++)
            {
                var child = transform.GetChild(i);
                children[i] = child.TryAutoCast() as Transform ?? child;
            }
            return children;
        }

        public static Component[] GetAllComponentsCasted(this GameObject gameObject)
        {
            return gameObject.GetComponents<Component>().Select(x => x.TryAutoCast() ?? x).OfType<Component>().ToArray();
        }

        public static Component[] GetAllComponentsCasted(this Component component)
        {
            return component.GetComponents<Component>().Select(x => x.TryAutoCast() ?? x).OfType<Component>().ToArray();
        }
        
        public static Component[] GetAllComponentsInChildrenCasted(this GameObject gameObject, bool includeInactive)
        {
            return gameObject.GetComponentsInChildren<Component>(includeInactive).Select(x => x.TryAutoCast() ?? x).OfType<Component>().ToArray();
        }
        
        public static Component[] GetAllComponentsInChildrenCasted(this Component component, bool includeInactive)
        {
            return component.GetComponentsInChildren<Component>(includeInactive).Select(x => x.TryAutoCast() ?? x).OfType<Component>().ToArray();
        }

        public static object? TryAutoCast(this Il2CppSystem.Object component)
        {
            var il2CppType = component.GetIl2CppType();

            var monoType = TryGetMonoType(il2CppType);
            if (monoType != null)
                return _mIl2CppObjectBaseCast.MakeGenericMethod(monoType).Invoke(component, null);

            return null;
        }

        private static readonly MethodInfo _mIl2CppObjectBaseCast = _mIl2CppObjectBaseCast = AccessTools.Method(typeof(Il2CppObjectBase), nameof(Il2CppObjectBase.Cast));
        private static readonly Dictionary<string, Type?> _cachedTypes = new();
        private static readonly HashSet<string> _cachedAssemblies = new();

        public static Type? TryGetMonoType(this Il2CppSystem.Type il2CppType)
        {
            var typeName = il2CppType.FullNameOrDefault;

            if (_cachedTypes.TryGetValue(typeName, out var monoType)) return monoType;

            // Look for newly loaded assemblies and add all types from them to the cache
            var newAssemblies = AccessTools.AllAssemblies().Where(x => x.FullName != null && _cachedAssemblies.Add(x.FullName)).ToList();
            //Console.WriteLine(string.Join("\n", newAssemblies.First(x=>x.FullName.Contains("ConfigurationM")).GetTypesSafe().Select(x=>x.FullName)));
            foreach (var types in newAssemblies /*.Where(x => x.FullName != "InjectedMonoTypes")*/
                                  .SelectMany(x => x.GetTypesSafe())
                                  .GroupBy(x => x.FullName!))
            {
                _cachedTypes[types.Key] = types.OrderByDescending(a =>
                {
                    var assemblyFullName = a.Assembly.FullName!;
                    return assemblyFullName.StartsWith("UnityEngine.") || assemblyFullName.StartsWith("System.");
                }).First();
            }

            if (_cachedTypes.TryGetValue(typeName, out monoType)) return monoType;

            // If we still can't find the type, try to find it by name and namespace. Nested types are missing the namespace part in IL2CPP Types.
            // todo try this first to be safe?
            var typeNameWithNamespace = il2CppType.Namespace + "." + typeName.Replace('.', '+');

            //Console.WriteLine(typeName);
            //Console.WriteLine(il2CppType.FormatTypeName());
            //Console.WriteLine(il2CppType.Namespace);

            if (_cachedTypes.TryGetValue(typeNameWithNamespace, out monoType))
            {
                _cachedTypes[typeName] = monoType;
                _cachedTypes[typeNameWithNamespace] = monoType;
                return monoType;
            }
            else
            {
                RuntimeUnityEditorCore.Logger.Log(LogLevel.Warning, "Failed to find any Mono Type matching an IL2CPP Type: " + il2CppType.AssemblyQualifiedName);
                _cachedTypes[typeName] = null;
                return null;
            }
        }
    }
}
