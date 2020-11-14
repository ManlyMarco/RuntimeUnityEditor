using System;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Reflection;
using UnityEngine;

namespace RuntimeUnityEditor.Core.Utils
{
    public static class UnityFeatureHelper
    {
        private static readonly Type _sceneManager = Type.GetType("UnityEngine.SceneManagement.SceneManager, UnityEngine", false);
        private static readonly Type _scene = Type.GetType("UnityEngine.SceneManagement.Scene, UnityEngine", false);
        private static readonly Type _vectrosity = Type.GetType("Vectrosity.VectorObject2D, Vectrosity", false);

        static UnityFeatureHelper()
        {
            SupportsScenes = _scene != null && _sceneManager != null;
            if (!SupportsScenes)
                RuntimeUnityEditorCore.Logger.Log(LogLevel.Warning, "UnityEngine.SceneManager and/or UnityEngine.SceneManagement.Scene are not available, some features will be disabled");

            SupportsCursorIndex = !(typeof(TextEditor).GetProperty("cursorIndex", BindingFlags.Instance | BindingFlags.Public) == null && typeof(TextEditor).GetField("pos", BindingFlags.Instance | BindingFlags.Public) == null);
            if (!SupportsCursorIndex)
                RuntimeUnityEditorCore.Logger.Log(LogLevel.Warning, "TextEditor.cursorIndex and TextEditor.pos are not available, some features will be disabled");

            SupportsVectrosity = _vectrosity != null;
            if (!SupportsVectrosity)
                RuntimeUnityEditorCore.Logger.Log(LogLevel.Warning, "Vectrosity.dll is not available, drawing gizmos will be disabled");

            if (SupportsCursorIndex)
            {
                SupportsRepl = true;
                try
                {
                    var profilerType = Type.GetType("MonoProfiler.MonoProfilerPatcher, MonoProfilerLoader", false)
                        ?.GetProperty("IsInitialized", BindingFlags.Static | BindingFlags.Public);
                    var profilerIsRunning = profilerType != null && (bool)profilerType.GetValue(null, null);

                    if (profilerIsRunning)
                    {
                        RuntimeUnityEditorCore.Logger.Log(LogLevel.Warning, "Disabling REPL because a profiler is running. This is to prevent the combination of access-modded mcs, profiler and monomod from crashing the process.");
                        SupportsRepl = false;
                    }
                }
                catch (Exception ex)
                {
                    RuntimeUnityEditorCore.Logger.Log(LogLevel.Error, ex);
                }
            }
        }

        public static bool SupportsScenes { get; private set; }
        public static bool SupportsCursorIndex { get; }
        public static bool SupportsRepl { get; }
        public static bool SupportsVectrosity { get; }

        public static GameObject[] GetSceneGameObjects()
        {
            try
            {
                return GetSceneGameObjectsInternal();
            }
            catch (Exception)
            {
                SupportsScenes = false;
                return new GameObject[0];
            }
        }

        public static GameObject[] GetSceneGameObjectsInternal()
        {
            // Reflection for compatibility with Unity 4.x
            var activeScene = _sceneManager.GetMethod("GetActiveScene", BindingFlags.Static | BindingFlags.Public);
            var scene = activeScene.Invoke(null, null);
            
            var rootGameObjects = scene.GetType().GetMethod("GetRootGameObjects", BindingFlags.Instance | BindingFlags.Public, null, new Type[]{}, null);
            var objects = rootGameObjects.Invoke(scene, null);

            return (GameObject[])objects;
        }

        public static void OpenLog()
        {
            bool TryOpen(string path)
            {
                if (!File.Exists(path)) return false;
                try
                {
                    Process.Start(path);
                    return true;
                }
                catch
                {
                    return false;
                }
            }

            // Redirected by preloader to game root
            if (TryOpen(Path.Combine(Path.Combine(Application.dataPath, ".."), "output_log.txt"))) return;

            // Generated in most versions unless disabled
            if (TryOpen(Path.Combine(Application.dataPath, "output_log.txt"))) return;

            // Available since 2018.3
            var prop = typeof(Application).GetProperty("consoleLogPath", BindingFlags.Static | BindingFlags.Public);
            if (prop != null)
            {
                var path = prop.GetValue(null, null) as string;
                if (TryOpen(path)) return;
            }

            if (Directory.Exists(Application.persistentDataPath))
            {
                var file = Directory.GetFiles(Application.persistentDataPath, "output_log.txt", SearchOption.AllDirectories).FirstOrDefault();
                if (TryOpen(file)) return;
            }

            // Fall back to more aggresive brute search
            var rootDir = Directory.GetParent(Application.dataPath);
            if (rootDir.Exists)
            {
                // BepInEx 5.x log file
                var result = rootDir.GetFiles("LogOutput.log", SearchOption.AllDirectories).FirstOrDefault();
                if (result == null)
                    result = rootDir.GetFiles("output_log.txt", SearchOption.AllDirectories).FirstOrDefault();

                if (result != null && TryOpen(result.FullName)) return;
            }

            RuntimeUnityEditorCore.Logger.Log(LogLevel.Message | LogLevel.Error, "No log files were found");
        }

        public static Texture2D LoadTexture(byte[] texData)
        {
            var tex = new Texture2D(1, 1, TextureFormat.ARGB32, false);

            // Around Unity 2018 the LoadImage and other export/import methods got moved from Texture2D to extension methods
            var loadMethod = typeof(Texture2D).GetMethod("LoadImage", new[] { typeof(byte[]) });
            if (loadMethod != null)
            {
                loadMethod.Invoke(tex, new object[] { texData });
            }
            else
            {
                var converter = Type.GetType("UnityEngine.ImageConversion, UnityEngine.ImageConversionModule");
                if (converter == null) throw new ArgumentNullException(nameof(converter));
                var converterMethod = converter.GetMethod("LoadImage", new[] { typeof(Texture2D), typeof(byte[]) });
                if (converterMethod == null) throw new ArgumentNullException(nameof(converterMethod));
                converterMethod.Invoke(null, new object[] { tex, texData });
            }

            return tex;
        }
    }
}
