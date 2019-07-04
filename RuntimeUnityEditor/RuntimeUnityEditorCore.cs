using System;
using RuntimeUnityEditor.Core.ObjectTree;
using RuntimeUnityEditor.Core.REPL;
using UnityEngine;

namespace RuntimeUnityEditor.Core
{
    public class RuntimeUnityEditorCore
    {
        public const string Version = "1.5";
        public const string GUID = "RuntimeUnityEditor";

        public Inspector.Inspector Inspector { get; }
        public ObjectTreeViewer TreeViewer { get; }
        public ReplWindow Repl { get; }

        public KeyCode ShowHotkey { get; set; } = KeyCode.F12;

        internal static RuntimeUnityEditorCore Instance { get; private set; }
        internal static GameObject PluginObject { get; private set; }
        internal static ILoggerWrapper Logger { get; private set; }

        public RuntimeUnityEditorCore(GameObject pluginObject, ILoggerWrapper logger)
        {
            if(Instance != null)
                throw new InvalidOperationException("Can only create one instance of the Core object");

            PluginObject = pluginObject;
            Logger = logger;
            Instance = this;

            Inspector = new Inspector.Inspector(targetTransform => TreeViewer.SelectAndShowObject(targetTransform));
            TreeViewer = new ObjectTreeViewer(items =>
            {
                Inspector.InspectorClear();
                foreach (var stackEntry in items)
                    Inspector.InspectorPush(stackEntry);
            });

            if (Utils.UnityFeatureHelper.SupportsCursorIndex &&
                Utils.UnityFeatureHelper.SupportsXml)
                Repl = new ReplWindow();
        }

        public void OnGUI()
        {
            if (Show)
            {
                Inspector.DisplayInspector();
                TreeViewer.DisplayViewer();
                Repl?.DisplayWindow();
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
            }
        }

        public void Update()
        {
            if (Input.GetKeyDown(ShowHotkey))
                Show = !Show;

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
            Repl?.UpdateWindowSize(new Rect(
                centerX,
                screenRect.yMin + inspectorHeight + replPadding,
                centerWidth,
                screenRect.height - inspectorHeight - replPadding));
        }
    }
}
