using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Reflection;
using UnityEngine;

namespace RuntimeUnityEditor.Core.Utils.Abstractions
{
    /// <summary>
    /// Abstractions for Unity engine features that got changed in some way across different engine version.
    /// </summary>
    public static class UnityFeatureHelper
    {
        private static readonly Type _sceneManager = Type.GetType("UnityEngine.SceneManagement.SceneManager, UnityEngine", false);
        private static readonly Type _scene = Type.GetType("UnityEngine.SceneManagement.Scene, UnityEngine", false);

        static UnityFeatureHelper()
        {
            SupportsScenes = _scene != null && _sceneManager != null;
            if (!SupportsScenes)
                RuntimeUnityEditorCore.Logger.Log(LogLevel.Warning, "UnityEngine.SceneManager and/or UnityEngine.SceneManagement.Scene are not available, some features will be disabled");

            SupportsCursorIndex = !(typeof(TextEditor).GetProperty("cursorIndex", BindingFlags.Instance | BindingFlags.Public) == null && typeof(TextEditor).GetField("pos", BindingFlags.Instance | BindingFlags.Public) == null);
            if (!SupportsCursorIndex)
                RuntimeUnityEditorCore.Logger.Log(LogLevel.Warning, "TextEditor.cursorIndex and TextEditor.pos are not available, some features will be disabled");

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

        /// <summary>
        /// UnityEngine.SceneManagement.SceneManager is available, used by <see cref="GetSceneGameObjects"/>.
        /// </summary>
        public static bool SupportsScenes { get; private set; }

        /// <summary>
        /// TextEditor.cursorIndex is available.
        /// </summary>
        public static bool SupportsCursorIndex { get; }

        /// <summary>
        /// C# REPL SHOULD be able to run in this environment (mcs might still be unhappy).
        /// </summary>
        public static bool SupportsRepl { get; }

        /// <summary>
        /// Get root game objects in active scene, or nothing if game doesn't support this.
        /// </summary>
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

        private static GameObject[] GetSceneGameObjectsInternal()
        {
            // Reflection for compatibility with Unity 4.x
            var activeScene = _sceneManager.GetMethod("GetActiveScene", BindingFlags.Static | BindingFlags.Public);
            if (activeScene == null) throw new ArgumentNullException(nameof(activeScene));
            var scene = activeScene.Invoke(null, null);
            
            var rootGameObjects = scene.GetType().GetMethod("GetRootGameObjects", BindingFlags.Instance | BindingFlags.Public, null, new Type[]{}, null);
            if (rootGameObjects == null) throw new ArgumentNullException(nameof(rootGameObjects));
            var objects = rootGameObjects.Invoke(scene, null);

            return (GameObject[])objects;
        }
        
        /// <summary>
        /// Figure out where the log file is written to and open it.
        /// </summary>
        public static void OpenLog()
        {
            bool TryOpen(string path)
            {
                if (path == null) return false;
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

            var candidates = new List<string>();

            // Redirected by preloader to game root
            var rootDir = Path.Combine(Application.dataPath, "..");
            candidates.Add(Path.Combine(rootDir, "output_log.txt"));

            // Generated in most versions unless disabled
            candidates.Add(Path.Combine(Application.dataPath, "output_log.txt"));

            // Available since 2018.3
            var prop = typeof(Application).GetProperty("consoleLogPath", BindingFlags.Static | BindingFlags.Public);
            if (prop != null)
            {
                var path = prop.GetValue(null, null) as string;
                candidates.Add(path);
            }

            if (Directory.Exists(Application.persistentDataPath))
            {
                var file = Directory.GetFiles(Application.persistentDataPath, "output_log.txt", SearchOption.AllDirectories).FirstOrDefault();
                candidates.Add(file);
            }

            var latestLog = candidates.Where(File.Exists).OrderByDescending(File.GetLastWriteTimeUtc).FirstOrDefault();
            if (TryOpen(latestLog)) return;

            candidates.Clear();
            // Fall back to more aggresive brute search
            // BepInEx 5.x log file, can be "LogOutput.log.1" or higher if multiple game instances run
            candidates.AddRange(Directory.GetFiles(rootDir,"LogOutput.log*", SearchOption.AllDirectories));
            candidates.AddRange(Directory.GetFiles(rootDir,"output_log.txt", SearchOption.AllDirectories));
            latestLog = candidates.Where(File.Exists).OrderByDescending(File.GetLastWriteTimeUtc).FirstOrDefault();
            if (TryOpen(latestLog)) return;

            throw new FileNotFoundException("No log files were found");
        }

        /// <summary>
        /// Abstraction for Texture2D.LoadImage. In later Unity versions it was moved into an extension method.
        /// </summary>
        public static Texture2D LoadTexture(byte[] texData)
        {
            var tex = new Texture2D(1, 1, TextureFormat.ARGB32, false);
            tex.LoadImage(texData, false);
            return tex;
        }
    }
}
