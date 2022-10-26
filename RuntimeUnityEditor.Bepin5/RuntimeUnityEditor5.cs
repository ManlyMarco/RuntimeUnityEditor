using System;
using BepInEx;
using BepInEx.Configuration;
using BepInEx.Logging;
using RuntimeUnityEditor.Core;
using UnityEngine;
using LogLevel = RuntimeUnityEditor.Core.LogLevel;

namespace RuntimeUnityEditor.Bepin5
{
    [BepInPlugin(RuntimeUnityEditorCore.GUID, "Runtime Unity Editor", RuntimeUnityEditorCore.Version)]
    public class RuntimeUnityEditor5 : BaseUnityPlugin
    {
        [Obsolete("No longer used", true)] public ConfigEntry<string> DnSpyPath { get; private set; }
        [Obsolete("No longer used", true)] public ConfigEntry<string> DnSpyArgs { get; private set; }
        [Obsolete("No longer used", true)] public ConfigEntry<bool> ShowRepl { get; private set; }
        [Obsolete("No longer used", true)] public ConfigEntry<bool> EnableMouseInspector { get; private set; }
        [Obsolete("No longer used", true)] public ConfigEntry<KeyboardShortcut> Hotkey { get; private set; }

        public static RuntimeUnityEditorCore Instance { get; private set; }

        private void Start()
        {
            Instance = new RuntimeUnityEditorCore(new Bep5InitSettings(this));
        }

        private void Update()
        {
            Instance.Update();
        }

        private void LateUpdate()
        {
            Instance.LateUpdate();
        }

        private void OnGUI()
        {
            Instance.OnGUI();
        }

        private sealed class Bep5InitSettings : RuntimeUnityEditorCore.InitSettings
        {
            private readonly RuntimeUnityEditor5 _instance;

            public Bep5InitSettings(RuntimeUnityEditor5 instance)
            {
                _instance = instance;
                LoggerWrapper = new Logger5(_instance.Logger);
            }

            public override Action<T> RegisterSetting<T>(string category, string name, T defaultValue, string description, Action<T> onValueUpdated)
            {
                var s = _instance.Config.Bind(category, name, defaultValue, description);
                s.SettingChanged += (sender, args) => onValueUpdated(s.Value);
                onValueUpdated(s.Value);
                return x => s.Value = x;
            }

            public override MonoBehaviour PluginMonoBehaviour => _instance;
            public override ILoggerWrapper LoggerWrapper { get; }
            public override string ConfigPath => Paths.ConfigPath;
        }

        private sealed class Logger5 : ILoggerWrapper
        {
            private readonly ManualLogSource _logger;

            public Logger5(ManualLogSource logger)
            {
                _logger = logger;
            }

            public void Log(LogLevel logLogLevel, object content)
            {
                _logger.Log((BepInEx.Logging.LogLevel)logLogLevel, content);
            }
        }
    }
}
