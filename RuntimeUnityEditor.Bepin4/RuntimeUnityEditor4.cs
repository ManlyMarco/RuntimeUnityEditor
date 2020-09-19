using System.ComponentModel;
using BepInEx;
using RuntimeUnityEditor.Core;

namespace RuntimeUnityEditor.Bepin4
{
    [BepInPlugin(RuntimeUnityEditorCore.GUID, "Runtime Unity Editor (Legacy)", RuntimeUnityEditorCore.Version)]
    public class RuntimeUnityEditor4 : BaseUnityPlugin
    {
        [DisplayName("Path to dnSpy.exe")]
        [Description(
            "Full path to dnSpy that will enable integration with Inspector.\n\n" +
            "When correctly configured, you will see a new ^ buttons that will open the members in dnSpy.")]
        public ConfigWrapper<string> DnSpyPath { get; private set; }

        public static RuntimeUnityEditorCore Instance { get; set; }

        private void OnGUI()
        {
            Instance.OnGUI();
        }

        private void Start()
        {
            Instance = new RuntimeUnityEditorCore(this, new Logger(), Paths.PluginPath);
            DnSpyPath = new ConfigWrapper<string>(nameof(DnSpyPath), this);
            DnSpyPath.SettingChanged += (sender, args) => DnSpyHelper.DnSpyPath = DnSpyPath.Value;
            DnSpyHelper.DnSpyPath = DnSpyPath.Value;
        }

        private void Update()
        {
            Instance.Update();
        }

        private void LateUpdate()
        {
            Instance.LateUpdate();
        }

        private sealed class Logger : ILoggerWrapper
        {
            public void Log(LogLevel logLogLevel, object content)
            {
                BepInEx.Logger.Log((BepInEx.Logging.LogLevel) logLogLevel, "[RuntimeEditor]" + content);
            }
        }
    }
}
