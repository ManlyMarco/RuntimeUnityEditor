using BepInEx;
using BepInEx.Configuration;
using BepInEx.Logging;
using RuntimeUnityEditor.Core;
using LogLevel = RuntimeUnityEditor.Core.LogLevel;

namespace RuntimeUnityEditor.Bepin5
{
    [BepInPlugin(RuntimeUnityEditorCore.GUID, "Runtime Unity Editor", RuntimeUnityEditorCore.Version)]
    public class RuntimeUnityEditor5 : BaseUnityPlugin
    {
        public ConfigWrapper<string> DnSpyPath { get; private set; }

        public static RuntimeUnityEditorCore Instance { get; private set; }

        private void OnGUI()
        {
            Instance.OnGUI();
        }

        private void Start()
        {
            Instance = new RuntimeUnityEditorCore(this, new Logger5(Logger));

            DnSpyPath = Config.Wrap(null, "Path to dnSpy.exe", "Full path to dnSpy that will enable integration with Inspector. When correctly configured, you will see a new ^ buttons that will open the members in dnSpy.", string.Empty);
            DnSpyPath.SettingChanged += (sender, args) => DnSpyHelper.DnSpyPath = DnSpyPath.Value;
            DnSpyHelper.DnSpyPath = DnSpyPath.Value;
        }

        private void Update()
        {
            Instance.Update();
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
                _logger.Log((BepInEx.Logging.LogLevel) logLogLevel, content);
            }
        }
    }
}
