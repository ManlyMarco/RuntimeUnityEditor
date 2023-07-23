using RuntimeUnityEditor.Core;
using RuntimeUnityEditor.Core.Utils.Abstractions;
using System;
using UnityEngine;
using UnityModManagerNet;

namespace RuntimeUnityEditor.UMM
{
    /// <summary>
    /// This is a loader plugin for UnityModManager.
    /// When referencing RuntimeUnityEditor from other code it's recommended to not reference this assembly and instead reference RuntimeUnityEditorCore directly.
    /// You can see if RuntimeUnityEditor has finished loading with <code>RuntimeUnityEditorCore.IsInitialized()</code>.
    /// </summary>
    [Obsolete("It's recommended to reference RuntimeUnityEditorCore directly")]
    public static class RuntimeUnityEditorUMM
    {
        public static bool Enabled { get; private set; }
        public static UnityModManager.ModEntry Instance { get; private set; }
        public static RuntimeUnityEditorCore CoreInstance { get; private set; }
        public static RuntimeUnityEditorSettings Settings;
        private static GameObject runtimeUnityEditorGO;
        private static MonoBehaviour RuntimeUnityEditor;

        public static bool Load(UnityModManager.ModEntry modEntry)
        {
            if (Instance != null)
            {
                Instance.Logger.Critical("Multiple copies of Runtime Unity Editor detected!");
                return false;
            }

            Instance = modEntry;

            Settings = RuntimeUnityEditorSettings.Load<RuntimeUnityEditorSettings>(Instance);
            Settings.Load(Instance);

            runtimeUnityEditorGO = new GameObject("RuntimeUnityEditor", typeof(RuntimeUnityEditorBehaviour));
            UnityEngine.Object.DontDestroyOnLoad(runtimeUnityEditorGO);

            Instance.OnGUI = OnGUI;
            Instance.OnSaveGUI = Settings.Save;

            return true;
        }

        private static void OnGUI(UnityModManager.ModEntry modEntry)
        {
            Settings.Draw(modEntry);
        }

        private class RuntimeUnityEditorBehaviour : MonoBehaviour
        {
            void Start()
            {
                RuntimeUnityEditor = this;
                CoreInstance = new RuntimeUnityEditorCore(new UMMInitSettings(Instance, Settings));
            }
            private void Update()
            {
                CoreInstance.Update();
            }

            private void LateUpdate()
            {
                CoreInstance.LateUpdate();
            }

            private void OnGUI()
            {
                CoreInstance.OnGUI();
            }
        }

        private sealed class UMMInitSettings : InitSettings
        {
            private readonly UnityModManager.ModEntry _instance;
            private RuntimeUnityEditorSettings _settings;

            public UMMInitSettings(UnityModManager.ModEntry instance, RuntimeUnityEditorSettings settings)
            {
                _instance = instance;
                _settings = settings;
                LoggerWrapper = new LoggerUMM(_instance.Logger);
            }

            public override Action<T> RegisterSetting<T>(string category, string name, T defaultValue, string description, Action<T> onValueUpdated)
            {
                RuntimeUnityEditorSettings.Setting<T> setting;
                if (!_settings.Get<T>(category, name, out setting))
                {
                    setting = new RuntimeUnityEditorSettings.Setting<T>(defaultValue);
                    _settings.Add(category, name, setting);
                }
                setting.OnChanged = onValueUpdated;
                onValueUpdated(setting.Value);
     
                return (newValue) => { setting.Value = newValue; onValueUpdated(newValue); };
            }

            public override MonoBehaviour PluginMonoBehaviour => RuntimeUnityEditor;
            public override ILoggerWrapper LoggerWrapper { get; }
            public override string ConfigPath => Instance.Path;
        }

        private sealed class LoggerUMM : ILoggerWrapper
        {
            private readonly UnityModManager.ModEntry.ModLogger _logger;

            public LoggerUMM(UnityModManager.ModEntry.ModLogger logger)
            {
                _logger = logger;
            }

            public void Log(Core.Utils.Abstractions.LogLevel logLevel, object content)
            {
                switch (logLevel)
                {
                    case LogLevel.None:
                    case LogLevel.Fatal:
                        if (content is Exception) _logger.LogException(content as Exception);
                        else _logger.Critical(content as string); 
                        break;
                    case LogLevel.Error:
                        if (content is Exception) _logger.LogException(content as Exception);
                        else _logger.Error(content as string);
                        break;
                    case LogLevel.Warning:
                        if (content is Exception) _logger.LogException(content as Exception);
                        else _logger.Warning(content as string);
                        break;
                    case LogLevel.Message:
                    case LogLevel.Info:
                    case LogLevel.Debug:
                        if (content is Exception) _logger.LogException(content as Exception);
                        else _logger.Log(content as string);
                        break;
                }
            }
        }
    }
}
