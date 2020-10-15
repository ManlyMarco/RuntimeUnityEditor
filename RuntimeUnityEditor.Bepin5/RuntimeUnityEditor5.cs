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
        public ConfigEntry<string> DnSpyPath { get; private set; }
        public ConfigEntry<string> DnSpyArgs { get; private set; }
        public ConfigEntry<bool> ShowRepl { get; private set; }
        public ConfigEntry<bool> EnableMouseInspector { get; private set; }
        public ConfigEntry<KeyboardShortcut> Hotkey { get; private set; }

        public static RuntimeUnityEditorCore Instance { get; private set; }

        private void OnGUI()
        {
            Instance.OnGUI();
        }

        private void Start()
        {
            Instance = new RuntimeUnityEditorCore(this, new Logger5(Logger), Paths.ConfigPath);

            DnSpyPath = Config.Bind("Inspector", "Path to dnSpy.exe", string.Empty, "Full path to dnSpy that will enable integration with Inspector. When correctly configured, you will see a new ^ buttons that will open the members in dnSpy.");
            DnSpyPath.SettingChanged += (sender, args) => DnSpyHelper.DnSpyPath = DnSpyPath.Value;
            DnSpyHelper.DnSpyPath = DnSpyPath.Value;

            DnSpyArgs = Config.Bind("Inspector", "Optional dnSpy arguments", string.Empty, "Additional parameters that are added to the end of each call to dnSpy.");
            DnSpyArgs.SettingChanged += (sender, args) => DnSpyHelper.DnSpyArgs = DnSpyArgs.Value;
            DnSpyHelper.DnSpyArgs = DnSpyArgs.Value;

            if (Instance.Repl != null)
            {
                ShowRepl = Config.Bind("General", "Show REPL console", true);
                ShowRepl.SettingChanged += (sender, args) => Instance.ShowRepl = ShowRepl.Value;
                Instance.ShowRepl = ShowRepl.Value;
            }

            EnableMouseInspector = Config.Bind("General", "Enable Mouse Inspector", true);
            EnableMouseInspector.SettingChanged += (sender, args) => Instance.EnableMouseInspect = EnableMouseInspector.Value;
            Instance.EnableMouseInspect = EnableMouseInspector.Value;

            Hotkey = Config.Bind("General", "Open/close runtime editor", new KeyboardShortcut(KeyCode.F12));
            Hotkey.SettingChanged += (sender, args) => Instance.ShowHotkey = Hotkey.Value.MainKey;
            Instance.ShowHotkey = Hotkey.Value.MainKey;

            Instance.SettingsChanged += (sender, args) =>
            {
                Hotkey.Value = new KeyboardShortcut(Instance.ShowHotkey);
                if (ShowRepl != null) ShowRepl.Value = Instance.ShowRepl;
                DnSpyArgs.Value = DnSpyHelper.DnSpyArgs;
                EnableMouseInspector.Value = Instance.EnableMouseInspect;
            };
        }

        private void Update()
        {
            Instance.Update();
        }

        private void LateUpdate()
        {
            Instance.LateUpdate();
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
