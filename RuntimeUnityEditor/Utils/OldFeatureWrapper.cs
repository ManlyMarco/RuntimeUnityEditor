/* Some code taken from https://github.com/bbepis/XUnity.AutoTranslator */

using System;
using System.Linq;
using BepInEx.Logging;
using UnityEngine;

namespace RuntimeUnityEditor.Utils
{
    public static class OldFeatureWrapper
    {
        private static Type FindType(string name)
        {
            return (from x in AppDomain.CurrentDomain.GetAssemblies()
                    select x.GetType(name, false) into x
                    where x != null
                    select x).FirstOrDefault();
        }

        public static readonly Type SceneManager = FindType("UnityEngine.SceneManager");
        public static readonly Type Scene = FindType("UnityEngine.SceneManagement.Scene");
        public static readonly Type Mcs = FindType("Mono.CSharp.InteractiveBase");
        public static readonly Type Xml = FindType("System.Xml.XmlComment");

        static OldFeatureWrapper()
        {
            SupportsScenes = Scene != null && SceneManager != null;
            if (!SupportsScenes)
                BepInEx.Logger.Log(LogLevel.Warning, "[RuntimeEditor] UnityEngine.SceneManager and/or UnityEngine.SceneManagement.Scene are not available, some features will be disabled");

            // Todo detect properly?
            SupportsCursorIndex = SupportsScenes;
            if (!SupportsCursorIndex)
                BepInEx.Logger.Log(LogLevel.Warning, "[RuntimeEditor] TextEditor.cursorIndex is not available, some features will be disabled");

            McsDetected = Mcs != null;
            if (!McsDetected)
                BepInEx.Logger.Log(LogLevel.Warning, "[RuntimeEditor] mcs.dll is not available, some features will be disabled");

            SupportsXml = Xml != null;
            if (!SupportsXml)
                BepInEx.Logger.Log(LogLevel.Warning, "[RuntimeEditor] System.XML.dll is not available, some features will be disabled");
        }

        public static bool McsDetected;
        public static bool SupportsScenes;
        public static bool SupportsXml;
        public static bool SupportsCursorIndex;

        public static GameObject[] GetSceneGameObjects()
        {
            return GetSceneGameObjectsInternal();
        }

        public static GameObject[] GetSceneGameObjectsInternal()
        {
            return UnityEngine.SceneManagement.SceneManager.GetActiveScene().GetRootGameObjects();
        }
    }
}