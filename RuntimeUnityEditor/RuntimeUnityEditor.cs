using System.ComponentModel;
using BepInEx;
using RuntimeUnityEditor.ObjectTree;
using UnityEngine;

namespace RuntimeUnityEditor
{
    [BepInPlugin("RuntimeUnityEditor", "Runtime Unity Editor", Version)]
    public class RuntimeUnityEditor : BaseUnityPlugin
    {
        public const string Version = "1.1";

        [DisplayName("Path to dnSpy.exe")]
        [Description("Full path to dnSpy that will enable integration with Inspector.\n\n" +
                     "When correctly configured, you will see a new ^ buttons that will open the members in dnSpy.")]
        public ConfigWrapper<string> DnSpyPath { get; private set; }

        public Inspector.Inspector Inspector { get; private set; }
        public ObjectTreeViewer TreeViewer { get; private set; }

        protected void Awake()
        {
            DnSpyPath = new ConfigWrapper<string>(nameof(DnSpyPath), this);
            DnSpyPath.SettingChanged += (sender, args) => DnSpyHelper.DnSpyPath = DnSpyPath.Value;
            DnSpyHelper.DnSpyPath = DnSpyPath.Value;

            Inspector = new Inspector.Inspector(targetTransform => TreeViewer.SelectAndShowObject(targetTransform));
            TreeViewer = new ObjectTreeViewer(items =>
            {
                Inspector.InspectorClear();
                foreach (var stackEntry in items)
                    Inspector.InspectorPush(stackEntry);
            });

            DnSpyPath = new ConfigWrapper<string>(nameof(DnSpyPath), this);
            DnSpyPath.SettingChanged += (sender, args) => DnSpyHelper.DnSpyPath = DnSpyPath.Value;
            DnSpyHelper.DnSpyPath = DnSpyPath.Value;
        }

        protected void OnGUI()
        {
            if(Show)
            {
                Inspector.DisplayInspector();
                TreeViewer.DisplayViewer();
            }
        }

        public bool Show
        {
            get => TreeViewer.Enabled;
            set
            {
                TreeViewer.Enabled = value;

                if (value)
                {
                    SetWindowSizes();

                    TreeViewer.UpdateCaches();
                }

                //CursorBlocker.DisableCameraControls = _show;
            }
        }

        protected void Update()
        {
            if (Input.GetKeyDown(KeyCode.F12))
            {
                Show = !Show;
            }

            Inspector.InspectorUpdate();
        }

        private void SetWindowSizes()
        {
            int w = Screen.width, h = Screen.height;
            _screenRect = new Rect(ScreenOffset, ScreenOffset, w - ScreenOffset * 2, h - ScreenOffset * 2);

            UpdateWindowSize(_screenRect);
        }

        private void UpdateWindowSize(Rect screenRect)
        {
            Inspector.UpdateWindowSize(screenRect);
            TreeViewer.UpdateWindowSize(screenRect);
        }

        private const int ScreenOffset = 20;
        private Rect _screenRect;
    }
}
