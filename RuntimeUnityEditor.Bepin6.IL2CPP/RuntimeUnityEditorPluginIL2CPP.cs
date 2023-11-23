using BepInEx;
using BepInEx.Configuration;
using BepInEx.Logging;
using BepInEx.Unity.IL2CPP;
using RuntimeUnityEditor.Core;
using RuntimeUnityEditor.Core.Utils.Abstractions;
using UnityEngine;

namespace RuntimeUnityEditor.Bepin6.IL2CPP
{

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
            
            _coreInstance = new RuntimeUnityEditorCore(new Bep5InitSettings(this));
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

        private sealed class Bep5InitSettings : InitSettings
        {
            private readonly RuntimeUnityEditorPluginIL2CPP _instance;
            private readonly RuntimeUnityEditorHelper _helper;

            public Bep5InitSettings(RuntimeUnityEditorPluginIL2CPP instance)
            {
                _instance = instance ?? throw new ArgumentNullException(nameof(instance));
                _helper = instance.AddComponent<RuntimeUnityEditorHelper>();
                LoggerWrapper = new Logger6(_instance.Log);
            }

            public override Action<T> RegisterSetting<T>(string category, string name, T defaultValue, string description, Action<T> onValueUpdated)
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
