using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Runtime.CompilerServices;
using UnityEngine;
using UnityEngine.SceneManagement;

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

            if (SupportsScenes)
            {
                try
                {
                    _ = SceneProxyMethods.GetSceneCount();
                }
                catch
                {
                    SupportsScenes = false;
                }
            }

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

        #region Scenes

        /// <summary>
        /// UnityEngine.SceneManagement is available, used by <see cref="GetActiveSceneGameObjects"/>.
        /// </summary>
        public static bool SupportsScenes { get; private set; }

        /// <summary>
        /// Get root game objects in active scene, or nothing if game doesn't support this.
        /// </summary>
        public static GameObject[] GetActiveSceneGameObjects()
        {
            return SupportsScenes ? SceneProxyMethods.GetActiveSceneGameObjects() : new GameObject[0];
        }

        /// <summary>
        /// Number of loaded scenes. This is 0 if UnityEngine.SceneManagement is not available.
        /// </summary>
        public static int sceneCount => SupportsScenes ? SceneProxyMethods.GetSceneCount() : 0;

        /// <summary>
        /// Get the name of the scene a GameObject is in. This is only available if UnityEngine.SceneManagement is available.
        /// Returns false if not available.
        /// </summary>
        public static bool GetSceneName(this GameObject go, out string sceneName)
        {
            if (SupportsScenes)
            {
                sceneName = SceneProxyMethods.GetSceneName(go);
                return true;
            }

            sceneName = null;
            return false;
        }

        /// <summary>
        /// Get the root game objects of a scene. This is only available if UnityEngine.SceneManagement is available.
        /// </summary>
        public static GameObject[] GetSceneRootObjects(int sceneLoadIndex)
        {
            if (!SupportsScenes) return new GameObject[0];
            return SceneProxyMethods.GetRootGameObjects(sceneLoadIndex);
        }

        /// <summary>
        /// Get the scene at the given index. This is only available if UnityEngine.SceneManagement is available.
        /// </summary>
        public static SceneWrapper GetSceneAt(int sceneLoadIndex)
        {
            if (!SupportsScenes) return default;
            return SceneProxyMethods.GetSceneAt(sceneLoadIndex);
        }

        /// <summary>
        /// Unload a scene by name. This is only available if UnityEngine.SceneManagement is available.
        /// </summary>
        public static bool UnloadScene(string sceneName)
        {
            if (!SupportsScenes) return false;
            return SceneProxyMethods.UnloadScene(sceneName);
        }

        /// <summary>
        /// Wrapper for UnityEngine.SceneManagement.Scene to avoid exceptions in old Unity versions (mostly 4.x) that do not support them.
        /// </summary>
        public readonly struct SceneWrapper
        {
            /// <summary>
            /// Construct a new SceneWrapper.
            /// </summary>
            public SceneWrapper(string name, int buildIndex, int rootCount, bool isLoaded, bool isDirty, string path)
            {
                this.name = name;
                this.buildIndex = buildIndex;
                this.rootCount = rootCount;
                this.isLoaded = isLoaded;
                this.isDirty = isDirty;
                this.path = path;
            }

#pragma warning disable CS1591 // Missing XML comment for publicly visible type or member
            public readonly string name;
            public readonly int buildIndex;
            public readonly int rootCount;
            public readonly bool isLoaded;
            public readonly bool isDirty;
            public readonly string path;
#pragma warning restore CS1591 // Missing XML comment for publicly visible type or member

            /// <inheritdoc/>
            public override string ToString()
            {
                return $"Name: {name}\nBuildIndex: {buildIndex}\nRootCount: {rootCount}\nIsLoaded: {isLoaded}\nIsDirty: {isDirty}\nPath: {path}";
            }
        }

        /// <summary>
        /// Proxy methods for SceneManager and Scene to avoid exceptions in old Unity versions (mostly 4.x) that do not support them.
        /// By fencing all references here and never calling them directly it's possible to have no reflection overhead when they are available.
        /// The NoInlining attribute is necessary for this to work reliably, because the JIT might inline the calls all the way back to the original caller, which creates an impossible to catch TypeLoadException.
        /// </summary>
        private static class SceneProxyMethods
        {
            [MethodImpl(MethodImplOptions.NoInlining)]
            public static SceneWrapper GetSceneAt(int sceneLoadIndex)
            {
                var scene = SceneManager.GetSceneAt(sceneLoadIndex);
                return new SceneWrapper(scene.name, scene.buildIndex, scene.rootCount, scene.isLoaded, scene.isDirty, scene.path);
            }

            [MethodImpl(MethodImplOptions.NoInlining)]
            public static GameObject[] GetRootGameObjects(int sceneLoadIndex)
            {
                var scene = SceneManager.GetSceneAt(sceneLoadIndex);
                return scene.GetRootGameObjects();
            }

            [MethodImpl(MethodImplOptions.NoInlining)]
            public static bool UnloadScene(string sceneName)
            {
                return SceneManager.UnloadScene(sceneName);
            }

            [MethodImpl(MethodImplOptions.NoInlining)]
            public static int GetSceneCount() => SceneManager.sceneCount;

            [MethodImpl(MethodImplOptions.NoInlining)]
            public static string GetSceneName(GameObject go)
            {
                return go.scene.name;
            }

            [MethodImpl(MethodImplOptions.NoInlining)]
            public static GameObject[] GetActiveSceneGameObjects()
            {
                return SceneManager.GetActiveScene().GetRootGameObjects();
            }
        }

        #endregion

        /// <summary>
        /// TextEditor.cursorIndex is available.
        /// </summary>
        public static bool SupportsCursorIndex { get; }

        /// <summary>
        /// C# REPL SHOULD be able to run in this environment (mcs might still be unhappy).
        /// </summary>
        public static bool SupportsRepl { get; }

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
                    try
                    {
                        Process.Start(new ProcessStartInfo(path) { UseShellExecute = true });
                    }
                    catch (Win32Exception)
                    {
                        if (File.Exists(path))
                            Process.Start(new ProcessStartInfo("notepad.exe", $"\"{path}\""));
                        else
                            throw;
                    }
                    return true;
                }
                catch (Exception ex)
                {
                    Console.WriteLine(ex);
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
            candidates.AddRange(Directory.GetFiles(rootDir, "LogOutput.log*", SearchOption.AllDirectories));
            candidates.AddRange(Directory.GetFiles(rootDir, "output_log.txt", SearchOption.AllDirectories));
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
#if IL2CPP
            tex.LoadImage(texData, false);
#else
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
#endif
            return tex;
        }

        /// <summary>
        /// Throws if not available
        /// </summary>
        public static string systemCopyBuffer
        {
            get => SystemCopyBufferProxy.systemCopyBuffer;
            set => SystemCopyBufferProxy.systemCopyBuffer = value;
        }

        /// <see cref="SceneProxyMethods"/>
        private static class SystemCopyBufferProxy
        {
            public static string systemCopyBuffer
            {
                [MethodImpl(MethodImplOptions.NoInlining)]
                get => GUIUtility.systemCopyBuffer;
                [MethodImpl(MethodImplOptions.NoInlining)]
                set => GUIUtility.systemCopyBuffer = value;
            }
        }

        /// <summary>
        /// Throws if Camera.onPreRender and Camera.onPostRender are not available.
        /// They are not available in Unity 4.x
        /// </summary>
        public static void EnsureCameraRenderEventsAreAvailable()
        {
            var cameraType = typeof(Camera);
            if (cameraType.GetField(nameof(Camera.onPreRender), BindingFlags.Static | BindingFlags.Public) == null || cameraType.GetField(nameof(Camera.onPostRender), BindingFlags.Static | BindingFlags.Public) == null)
                throw new NotSupportedException("Camera.onPreRender and/or Camera.onPostRender are not available");
        }

        /// <summary>
        /// Create a copy of a Unity object. Uses reflection to avoid issues with old Unity versions.
        /// </summary>
        public static T InstantiateUnityObject<T>(T original) where T : UnityEngine.Object
        {
#if IL2CPP
            return UnityEngine.Object.Instantiate(original);
#else
            // Reflection because unity 4.x refuses to instantiate if built with newer versions of UnityEngine
            var methodInfo = typeof(UnityEngine.Object).GetMethod("Instantiate", BindingFlags.Static | BindingFlags.Public, null, new[] { typeof(UnityEngine.Object) }, null);
            if (methodInfo == null) throw new ArgumentNullException(nameof(methodInfo));
            return methodInfo.Invoke(null, new object[] { original }) as T;
#endif
        }
    }
}
