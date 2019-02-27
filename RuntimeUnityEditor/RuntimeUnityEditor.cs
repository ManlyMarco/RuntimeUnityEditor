using System.ComponentModel;
using BepInEx;
using RuntimeUnityEditor.ObjectTree;
using RuntimeUnityEditor.REPL;
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
        public ReplWindow Repl { get; private set; }

        internal static RuntimeUnityEditor Instance { get; private set; }
        
        protected void Awake()
        {
            Instance = this;

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

            Repl = new ReplWindow();

            DnSpyPath = new ConfigWrapper<string>(nameof(DnSpyPath), this);
            DnSpyPath.SettingChanged += (sender, args) => DnSpyHelper.DnSpyPath = DnSpyPath.Value;
            DnSpyHelper.DnSpyPath = DnSpyPath.Value;
        }

        protected void OnGUI()
        {
            if (Show)
            {
                Inspector.DisplayInspector();
                TreeViewer.DisplayViewer();
                Repl.DisplayWindow();
            }
        }

        [Browsable(false)]
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
            const int screenOffset = 10;

            var screenRect = new Rect(
                screenOffset,
                screenOffset,
                Screen.width - screenOffset * 2,
                Screen.height - screenOffset * 2);

            var centerWidth = (int)Mathf.Min(850, screenRect.width);
            var centerX = (int)(screenRect.xMin + screenRect.width / 2 - Mathf.RoundToInt((float)centerWidth / 2));

            var inspectorHeight = (int)(screenRect.height / 4) * 3;
            Inspector.UpdateWindowSize(new Rect(
                centerX, 
                screenRect.yMin, 
                centerWidth, 
                inspectorHeight));

            var rightWidth = 350;
            var treeViewHeight = screenRect.height;
            TreeViewer.UpdateWindowSize(new Rect(
                screenRect.xMax - rightWidth, 
                screenRect.yMin, 
                rightWidth, 
                treeViewHeight));

            var replPadding = 8;
            Repl.UpdateWindowSize(new Rect(
                centerX, 
                screenRect.yMin + inspectorHeight + replPadding, 
                centerWidth, 
                screenRect.height - inspectorHeight - replPadding));
        }
    }
}
