using System;
using System.Collections.Generic;
using System.Linq;
using BepInEx.Logging;
using UnityEngine;

namespace RuntimeUnityEditor.Utils
{
    public static class UnityFeatureHelper
    {
        private static readonly Type _sceneManager = Type.GetType("UnityEngine.SceneManagement.SceneManager, UnityEngine, Version=0.0.0.0, Culture=neutral, PublicKeyToken=null", false);
        private static readonly Type _scene = Type.GetType("UnityEngine.SceneManagement.Scene, UnityEngine, Version=0.0.0.0, Culture=neutral, PublicKeyToken=null", false);
        private static readonly Type _xml = Type.GetType("System.Xml.XmlComment, System.Xml, Version=4.0.0.0, Culture=neutral, PublicKeyToken=b77a5c561934e089", false);

        static UnityFeatureHelper()
        {
            SupportsScenes = _scene != null && _sceneManager != null;
            if (!SupportsScenes)
                BepInEx.Logger.Log(LogLevel.Warning, "[RuntimeEditor] UnityEngine.SceneManager and/or UnityEngine.SceneManagement.Scene are not available, some features will be disabled");

            // Todo detect properly?
            SupportsCursorIndex = SupportsScenes;
            if (!SupportsCursorIndex)
                BepInEx.Logger.Log(LogLevel.Warning, "[RuntimeEditor] TextEditor.cursorIndex is not available, some features will be disabled");

            SupportsXml = _xml != null;
            if (!SupportsXml)
                BepInEx.Logger.Log(LogLevel.Warning, "[RuntimeEditor] System.XML.dll is not available, some features will be disabled");
        }
        
        public static bool SupportsScenes { get; private set; }
        public static bool SupportsXml { get; }
        public static bool SupportsCursorIndex { get; }

        public static IEnumerable<GameObject> GetSceneGameObjects()
        {
            try
            {
                return GetSceneGameObjectsInternal();
            }
            catch (Exception)
            {
                SupportsScenes = false;
                return Enumerable.Empty<GameObject>();
            }
        }

        public static GameObject[] GetSceneGameObjectsInternal()
        {
            return UnityEngine.SceneManagement.SceneManager.GetActiveScene().GetRootGameObjects();
        }
    }
}
