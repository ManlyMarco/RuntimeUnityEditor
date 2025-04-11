using System;
using RuntimeUnityEditor.Core;
using RuntimeUnityEditor.Core.Utils.Abstractions;
using UnityEngine;
using UnityModManagerNet;

#pragma warning disable CS1591 // Missing XML comment for publicly visible type or member
#pragma warning disable IDE0051 // Remove unused private members

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
        public static bool Enabled
        {
            get => _editorMonoBehaviour?.enabled ?? false;
            private set => _editorMonoBehaviour.enabled = value;
        }
        public static UnityModManager.ModEntry Instance { get; private set; }
        public static RuntimeUnityEditorCore CoreInstance { get; private set; }
        public static RuntimeUnityEditorSettings Settings { get; private set; }

        private static GameObject _editorGameObject;
        private static MonoBehaviour _editorMonoBehaviour;

        public static bool Load(UnityModManager.ModEntry modEntry)
        {
            if (Instance != null)
            {
                Instance.Logger.Critical("Multiple copies of Runtime Unity Editor detected!");
                return false;
            }

            Instance = modEntry;

            Settings = UnityModManager.ModSettings.Load<RuntimeUnityEditorSettings>(Instance);
            Settings.Load(Instance);

            _editorGameObject = new GameObject("RuntimeUnityEditor", typeof(RuntimeUnityEditorBehaviour));
            UnityEngine.Object.DontDestroyOnLoad(_editorGameObject);

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
            private void Start()
            {
                _editorMonoBehaviour = this;
                CoreInstance = new RuntimeUnityEditorCore(new UMMInitSettings(Instance, Settings));
            }

            private void Update() => CoreInstance.Update();

            private void LateUpdate() => CoreInstance.LateUpdate();

            private void OnGUI() => CoreInstance.OnGUI();
        }

        private sealed class UMMInitSettings : InitSettings
        {
            private readonly RuntimeUnityEditorSettings _settings;

            public UMMInitSettings(UnityModManager.ModEntry instance, RuntimeUnityEditorSettings settings)
            {
                _settings = settings;
                LoggerWrapper = new LoggerUMM(instance.Logger);
            }

            protected override Action<T> RegisterSetting<T>(string category, string name, T defaultValue, string description, Action<T> onValueUpdated)
            {
                if (!_settings.Get<T>(category, name, out var setting))
                {
                    setting = new RuntimeUnityEditorSettings.Setting<T>(defaultValue);
                    _settings.Add(category, name, setting);
                }
                setting.OnChanged = onValueUpdated;
                onValueUpdated(setting.Value);

                return (newValue) => { setting.Value = newValue; onValueUpdated(newValue); };
            }

            public override MonoBehaviour PluginMonoBehaviour => _editorMonoBehaviour;
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
                    case LogLevel.All:
                    case LogLevel.None:
                    case LogLevel.Fatal:
                        if (content is Exception e1) _logger.LogException(e1);
                        else _logger.Critical(content as string);
                        break;

                    case LogLevel.Error:
                        if (content is Exception e2) _logger.LogException(e2);
                        else _logger.Error(content as string);
                        break;

                    case LogLevel.Warning:
                        if (content is Exception e3) _logger.LogException(e3);
                        else _logger.Warning(content as string);
                        break;

                    case LogLevel.Message:
                    case LogLevel.Info:
                    case LogLevel.Debug:
                        if (content is Exception e4) _logger.LogException(e4);
                        else _logger.Log(content as string);
                        break;

                    default:
                        _logger.Warning($"Unknown LogLevel [{logLevel}] for content: {content}");
                        break;
                }
            }
        }
    }
}
