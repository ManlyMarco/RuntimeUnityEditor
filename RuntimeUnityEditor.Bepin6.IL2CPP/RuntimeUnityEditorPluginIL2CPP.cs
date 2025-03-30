using BepInEx;
using BepInEx.Configuration;
using BepInEx.Logging;
using BepInEx.Unity.IL2CPP;
using RuntimeUnityEditor.Bepin6.LogViewer;
using RuntimeUnityEditor.Core;
using RuntimeUnityEditor.Core.Utils.Abstractions;
using UnityEngine;

namespace RuntimeUnityEditor.Bepin6.IL2CPP
{
    /// <summary>
    /// This is a loader plugin for BepInEx6 (IL2CPP version).
    /// When referencing RuntimeUnityEditor from other code it's recommended to not reference this assembly and instead reference RuntimeUnityEditorCore directly.
    /// If you need your code to run after RUE is initialized, add a <code>[BepInDependency(RuntimeUnityEditorCore.GUID)]</code> attribute to your plugin.
    /// You can see if RuntimeUnityEditor has finished loading with <code>RuntimeUnityEditorCore.IsInitialized()</code>.
    /// </summary>
    [Obsolete("It's recommended to reference RuntimeUnityEditorCore directly")]
    [BepInPlugin(RuntimeUnityEditorCore.GUID, "Runtime Unity Editor", RuntimeUnityEditorCore.Version)]
    public class RuntimeUnityEditorPluginIL2CPP : BasePlugin
    {
        private static RuntimeUnityEditorCore _coreInstance = null!;

        public override void Load()
        {
            if (!TomlTypeConverter.CanConvert(typeof(Rect)))
            {
                var converter = Core.Utils.TomlTypeConverter.GetConverter(typeof(Rect));
                TomlTypeConverter.AddConverter(typeof(Rect), new TypeConverter { ConvertToObject = converter.ConvertToObject, ConvertToString = converter.ConvertToString });
            }
            
            _coreInstance = new RuntimeUnityEditorCore(new Bep6InitSettings(this));

            _coreInstance.AddFeature(new LogViewerWindow());
        }

        private class RuntimeUnityEditorHelper : MonoBehaviour
        {

            private void Update()
            {
                _coreInstance.Update();
            }

            private void LateUpdate()
            {
                _coreInstance.LateUpdate();
            }

            private void OnGUI()
            {
                _coreInstance.OnGUI();
            }
        }

        private sealed class Bep6InitSettings : InitSettings
        {
            private readonly RuntimeUnityEditorPluginIL2CPP _instance;
            private readonly RuntimeUnityEditorHelper _helper;

            public Bep6InitSettings(RuntimeUnityEditorPluginIL2CPP instance)
            {
                _instance = instance ?? throw new ArgumentNullException(nameof(instance));
                _helper = instance.AddComponent<RuntimeUnityEditorHelper>();
                LoggerWrapper = new Logger6(_instance.Log);
            }

            protected override Action<T> RegisterSetting<T>(string category, string name, T defaultValue, string description, Action<T> onValueUpdated)
            {
                var s = _instance.Config.Bind(category, name, defaultValue, description);
                s.SettingChanged += (sender, args) => onValueUpdated(s.Value);
                onValueUpdated(s.Value);
                return x => s.Value = x;
            }

            public override MonoBehaviour PluginMonoBehaviour => _helper;
            public override ILoggerWrapper LoggerWrapper { get; }
            public override string ConfigPath => Paths.ConfigPath;
        }

        private sealed class Logger6 : ILoggerWrapper
        {
            private readonly ManualLogSource _logger;

            public Logger6(ManualLogSource logger)
            {
                _logger = logger;
            }

            public void Log(Core.Utils.Abstractions.LogLevel logLevel, object content)
            {
                _logger.Log((BepInEx.Logging.LogLevel)logLevel, content);
            }
        }
    }
}
