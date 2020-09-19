using System;
using System.Collections;
using System.Linq;
using System.Reflection;
using UnityEngine;

namespace RuntimeUnityEditor.Core.Utils
{
    internal static class AssetBundleManagerHelper
    {
        private static readonly IDictionary _abCacheManifestDic;
        private static readonly MethodInfo _abCacheUnloadMethod;

        static AssetBundleManagerHelper()
        {
            var abmType = AppDomain.CurrentDomain.GetAssemblies().SelectMany(x => x.GetTypesSafe()).FirstOrDefault(x => x.Name == "AssetBundleManager");
            if (abmType != null)
            {
                // public static Dictionary<string, AssetBundleManager.BundlePack> ManifestBundlePack {get;}
                var mbpProp = abmType.GetProperty("ManifestBundlePack", BindingFlags.Static | BindingFlags.Public);
                _abCacheManifestDic = mbpProp?.GetValue(null, null) as IDictionary;

                if (_abCacheManifestDic != null)
                {
                    // public static void UnloadAssetBundle(string assetBundleName, bool isUnloadForceRefCount, string manifestAssetBundleName = null, bool unloadAllLoadedObjects = false)
                    _abCacheUnloadMethod = abmType.GetMethod("UnloadAssetBundle", BindingFlags.Static | BindingFlags.Public, null, new[] { typeof(string), typeof(bool), typeof(string), typeof(bool) }, null);
                }
            }
        }

        public static void DrawButtonIfAvailable()
        {
            if (_abCacheUnloadMethod != null && GUILayout.Button("Clear Bundle Cache"))
            {
                try
                {
                    var unloadedCount = ClearAssetBundleCache();
                    RuntimeUnityEditorCore.Logger.Log(LogLevel.Message, "Unloaded " + unloadedCount + " AssetBundles");
                }
                catch (Exception e)
                {
                    RuntimeUnityEditorCore.Logger.Log(LogLevel.Message | LogLevel.Error, "Failed to clear the AssetBundle cache - " + e);
                }
            }
        }

        private static int ClearAssetBundleCache()
        {
            var unloadedCount = 0;

            var manifestDicEnumerator = _abCacheManifestDic.GetEnumerator();
            while (manifestDicEnumerator.MoveNext())
            {
                var valueType = manifestDicEnumerator.Value.GetType();
                // public Dictionary<string, LoadedAssetBundle> LoadedAssetBundles {get; set;}
                var loadedBundlesProp = valueType.GetProperty("LoadedAssetBundles", BindingFlags.Instance | BindingFlags.Public);
                var loadedBundlesDic = loadedBundlesProp?.GetValue(manifestDicEnumerator.Value, null) as IDictionary;

                if (loadedBundlesDic == null) throw new InvalidOperationException("Failed to get LoadedAssetBundles dictionary");

                // Need a copy of keys because unloading removes them
                foreach (var labsKey in loadedBundlesDic.Keys.Cast<string>().ToList())
                {
                    _abCacheUnloadMethod.Invoke(null, new[] { labsKey, true, manifestDicEnumerator.Key, false });
                    unloadedCount++;
                }
            }

            return unloadedCount;
        }
    }
}
