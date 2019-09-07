using System;
using System.IO;
using RuntimeUnityEditor.Core.Gizmos;
using RuntimeUnityEditor.Core.ObjectTree;
using RuntimeUnityEditor.Core.REPL;
using RuntimeUnityEditor.Core.Utils;
using UnityEngine;

namespace RuntimeUnityEditor.Core
{
    public class RuntimeUnityEditorCore
    {
        public const string Version = "1.8";
        public const string GUID = "RuntimeUnityEditor";

        public Inspector.Inspector Inspector { get; }
        public ObjectTreeViewer TreeViewer { get; }
        public ReplWindow Repl { get; }

        public KeyCode ShowHotkey { get; set; } = KeyCode.F12;

        internal static RuntimeUnityEditorCore Instance { get; private set; }
        internal static MonoBehaviour PluginObject { get; private set; }
        internal static ILoggerWrapper Logger { get; private set; }

        internal static GizmoDrawer GizmoDrawer { get; private set; }

        private CursorLockMode _previousCursorLockState;
        private bool _previousCursorVisible;
        private GUISkin _customSkin;

        internal RuntimeUnityEditorCore(MonoBehaviour pluginObject, ILoggerWrapper logger, string configPath)
        {
            if (Instance != null)
                throw new InvalidOperationException("Can only create one instance of the Core object");

            PluginObject = pluginObject;
            Logger = logger;
            Instance = this;

            Inspector = new Inspector.Inspector(targetTransform => TreeViewer.SelectAndShowObject(targetTransform));

            TreeViewer = new ObjectTreeViewer(pluginObject);
            TreeViewer.InspectorOpenCallback = items =>
            {
                Inspector.InspectorClear();
                foreach (var stackEntry in items)
                    Inspector.InspectorPush(stackEntry);
            };

            if (UnityFeatureHelper.SupportsVectrosity)
            {
                GizmoDrawer = new GizmoDrawer(pluginObject);
                TreeViewer.TreeSelectionChangedCallback = transform => GizmoDrawer.UpdateState(transform);
            }

            if (UnityFeatureHelper.SupportsCursorIndex &&
                UnityFeatureHelper.SupportsXml)
            {
                try
                {
                    Repl = new ReplWindow(Path.Combine(configPath, "RuntimeUnityEditor.Autostart.cs"));
                    Repl.RunAutostart();
                }
                catch (Exception ex)
                {
                    Logger.Log(LogLevel.Warning, "Failed to load REPL - " + ex.Message);
                }
            }
        }

        internal void OnGUI()
        {
            if (Show)
            {
                if (_customSkin == null)
                {
                    try
                    {
                        _customSkin = EditorUtilities.CreateSkin();
                    }
                    catch (Exception ex)
                    {
                        Logger.Log(LogLevel.Warning, "Could not load custom GUISkin - " + ex.Message);
                        _customSkin = GUI.skin;
                    }
                }

                var originalSkin = GUI.skin;
                GUI.skin = _customSkin;

                Cursor.lockState = CursorLockMode.None;
                Cursor.visible = true;

                Inspector.DisplayInspector();
                TreeViewer.DisplayViewer();
                Repl?.DisplayWindow();

                GUI.skin = originalSkin;
            }
        }

        public bool Show
        {
            get => TreeViewer.Enabled;
            set
            {
                if (Show != value)
                {
                    if (value)
                    {
                        _previousCursorLockState = Cursor.lockState;
                        _previousCursorVisible = Cursor.visible;
                    }
                    else
                    {
                        Cursor.lockState = _previousCursorLockState;
                        Cursor.visible = _previousCursorVisible;
                    }
                }

                TreeViewer.Enabled = value;

                if (GizmoDrawer != null)
                {
                    GizmoDrawer.Show = value;
                    GizmoDrawer.UpdateState(TreeViewer.SelectedTransform);
                }

                if (value)
                {
                    SetWindowSizes();

                    TreeViewer.UpdateCaches();
                }
            }
        }

        internal void Update()
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
