#if IL2CPP
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using BepInEx.Unity.IL2CPP;
using HarmonyLib;
using Il2CppInterop.Common.Attributes;
using Il2CppInterop.Runtime.InteropTypes;
using RuntimeUnityEditor.Core.Utils;
using RuntimeUnityEditor.Core.Utils.Abstractions;

namespace RuntimeUnityEditor.Core
{
    internal static class IL2CppExtensions
    {
        public static object TryAutoCast(this Il2CppSystem.Object component)
        {
            if (component == null) throw new ArgumentNullException(nameof(component));

            try
            {
                var il2CppType = component.GetIl2CppType();

                var monoType = TryGetMonoType(il2CppType);
                if (monoType != null)
                    return _mIl2CppObjectBaseCast.MakeGenericMethod(monoType).Invoke(component, null);
            }
            catch (Exception e)
            {
                RuntimeUnityEditorCore.Logger.Log(LogLevel.Warning, $"TryAutoCast failed for type {component.GetType().FullName}: {e}");
            }

            return null;
        }

        private static readonly MethodInfo _mIl2CppObjectBaseCast = _mIl2CppObjectBaseCast = AccessTools.Method(typeof(Il2CppObjectBase), nameof(Il2CppObjectBase.Cast));
        private static readonly Dictionary<string, Type> _cachedTypes = new Dictionary<string, Type>();
        private static readonly HashSet<string> _cachedAssemblies = new HashSet<string>();

        public static Type TryGetMonoType(this Il2CppSystem.Type il2CppType)
        {
            var typeName = il2CppType.FullName;

            if (_cachedTypes.TryGetValue(typeName, out var monoType)) return monoType;

            // Only mono interop types that are statically referenced are automatically loaded
            // Since RUE is entirely using reflection, we sometimes need to manually load the interop assemblies
            if (!_cachedAssemblies.Contains(il2CppType.Assembly.FullName))
            {
                try
                {
                    // TODO Remove all direct dependencies on BepInEx
                    // Only available since bleeding edge build #680
                    var interopAssPath = AccessTools.PropertyGetter(typeof(IL2CPPChainloader).Assembly.GetType("BepInEx.Unity.IL2CPP.Il2CppInteropManager"), "IL2CPPInteropAssemblyPath")?.Invoke(null, null) as string;
                    if (interopAssPath == null) throw new InvalidOperationException("Interop assembly path is not available");

                    var shortAssName = il2CppType.Assembly.GetName().Name;

                    var assPath = Path.Combine(interopAssPath, shortAssName + ".dll");
                    if (File.Exists(assPath))
                    {
                        RuntimeUnityEditorCore.Logger.Log(LogLevel.Info, "Loading interop assembly from " + assPath);
                        Assembly.LoadFile(assPath);
                    }
                }
                catch (Exception e)
                {
                    Console.WriteLine(e);
                }
            }

            // Look for newly loaded assemblies and add all types from them to the cache
            var newAssemblies = AccessTools.AllAssemblies().Where(x => x.FullName != null && _cachedAssemblies.Add(x.FullName)).ToList();
            //Console.WriteLine(string.Join("\n", newAssemblies.First(x=>x.FullName.Contains("ConfigurationM")).GetTypesSafe().Select(x=>x.FullName)));
            foreach (var types in newAssemblies /*.Where(x => x.FullName != "InjectedMonoTypes")*/
                                  .SelectMany(x => x.GetTypesSafe())
                                  .GroupBy(x => x.FullName))
            {
                var type = types.OrderByDescending(a =>
                {
                    var assemblyFullName = a.Assembly.FullName;
                    return assemblyFullName.StartsWith("UnityEngine.") || assemblyFullName.StartsWith("System.");
                }).First();
                var key = types.Key;
                var obfuscatedNameAttribute = type.GetCustomAttribute<ObfuscatedNameAttribute>();
                if (obfuscatedNameAttribute != null) key = obfuscatedNameAttribute.ObfuscatedName;
                _cachedTypes[key] = type;
            }

            if (_cachedTypes.TryGetValue(typeName, out monoType)) return monoType;

            // If we still can't find the type, try to find it by name and namespace. Nested types are missing the namespace part in IL2CPP Types.
            // todo try this first to be safe?
            var typeNameWithNamespace = il2CppType.Namespace + "." + typeName.Replace('.', '+');

            // RuntimeUnityEditorCore.Logger.Log(LogLevel.Warning, typeName);
            // RuntimeUnityEditorCore.Logger.Log(LogLevel.Warning, il2CppType.FormatTypeName());
            // RuntimeUnityEditorCore.Logger.Log(LogLevel.Warning, il2CppType.Namespace);
            // RuntimeUnityEditorCore.Logger.Log(LogLevel.Warning, il2CppType.Assembly.FullName);

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
#endif
