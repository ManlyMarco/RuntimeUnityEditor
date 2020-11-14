using System;
using System.Collections;
using System.IO;
using System.Reflection;
using RuntimeUnityEditor.Core.Gizmos;
using RuntimeUnityEditor.Core.ObjectTree;
using RuntimeUnityEditor.Core.REPL;
using RuntimeUnityEditor.Core.UI;
using RuntimeUnityEditor.Core.Utils;
using UnityEngine;

namespace RuntimeUnityEditor.Core
{
    public class RuntimeUnityEditorCore
    {
        public const string Version = "2.2.1";
        public const string GUID = "RuntimeUnityEditor";

        public Inspector.Inspector Inspector { get; }
        public ObjectTreeViewer TreeViewer { get; }
        public ReplWindow Repl { get; private set; }

        public event EventHandler SettingsChanged;

        public KeyCode ShowHotkey
        {
            get => _showHotkey;
            set
            {
                if (_showHotkey != value)
                {
                    _showHotkey = value;
                    SettingsChanged?.Invoke(this, EventArgs.Empty);
                }
            }
        }

        public bool ShowRepl
        {
            get => Repl != null && Repl.Show;
            set
            {
                if (Repl != null && Repl.Show != value)
                {
                    Repl.Show = value;
                    SettingsChanged?.Invoke(this, EventArgs.Empty);
                }
            }
        }

        public bool EnableMouseInspect
        {
            get => MouseInspect.Enable;
            set
            {
                if (MouseInspect.Enable != value)
                {
                    MouseInspect.Enable = value;
                    SettingsChanged?.Invoke(this, EventArgs.Empty);
                }
            }
        }

        public bool ShowInspector
        {
            get => Inspector != null && Inspector.Show;
            set
            {
                if (Inspector != null && Inspector.Show != value)
                {
                    Inspector.Show = value;
                    SettingsChanged?.Invoke(this, EventArgs.Empty);
                }
            }
        }

        public static RuntimeUnityEditorCore Instance { get; private set; }
        internal static MonoBehaviour PluginObject { get; private set; }
        internal static ILoggerWrapper Logger { get; private set; }

        private readonly GizmoDrawer _gizmoDrawer;
        private readonly GameObjectSearcher _gameObjectSearcher = new GameObjectSearcher();

        private bool _obsoleteCursor;
        
        private PropertyInfo _curLockState;
        private PropertyInfo _curVisible;
        
        private int _previousCursorLockState;
        private bool _previousCursorVisible;
        
        private KeyCode _showHotkey = KeyCode.F12;

        internal RuntimeUnityEditorCore(MonoBehaviour pluginObject, ILoggerWrapper logger, string configPath)
        {
            if (Instance != null)
                throw new InvalidOperationException("Can only create one instance of the Core object");

            PluginObject = pluginObject;
            Logger = logger;
            Instance = this;

            // Reflection for compatibility with Unity 4.x
            var tCursor = typeof(Cursor);
            
            _curLockState = tCursor.GetProperty("lockState", BindingFlags.Static | BindingFlags.Public);
            _curVisible = tCursor.GetProperty("visible", BindingFlags.Static | BindingFlags.Public);

            if (_curLockState == null && _curVisible == null)
            {
                _obsoleteCursor = true;
                
                _curLockState = typeof(Screen).GetProperty("lockCursor", BindingFlags.Static | BindingFlags.Public);
                _curVisible = typeof(Screen).GetProperty("showCursor", BindingFlags.Static | BindingFlags.Public);
            }
            
            Inspector = new Inspector.Inspector(targetTransform => TreeViewer.SelectAndShowObject(targetTransform));

            TreeViewer = new ObjectTreeViewer(pluginObject, _gameObjectSearcher);
            TreeViewer.InspectorOpenCallback = items =>
            {
                for (var i = 0; i < items.Length; i++)
                {
                    var stackEntry = items[i];
                    Inspector.Push(stackEntry, i == 0);
                }
            };

            if (UnityFeatureHelper.SupportsVectrosity)
            {
                _gizmoDrawer = new GizmoDrawer(pluginObject);
                TreeViewer.TreeSelectionChangedCallback = transform => _gizmoDrawer.UpdateState(transform);
            }

            if (UnityFeatureHelper.SupportsRepl)
            {
                try
                {
                    Repl = new ReplWindow(Path.Combine(configPath, "RuntimeUnityEditor.Autostart.cs"));
                    PluginObject.StartCoroutine(DelayedReplSetup());
                }
                catch (Exception ex)
                {
                    Logger.Log(LogLevel.Warning, "Failed to load REPL - " + ex.Message);
                    Repl = null;
                }
            }
        }

        private IEnumerator DelayedReplSetup()
        {
            yield return null;
            try
            {
                Repl.RunEnvSetup();
            }
            catch (Exception ex)
            {
                Logger.Log(LogLevel.Warning, "Failed to load REPL - " + ex.Message);
                Repl = null;
            }
        }

        internal void OnGUI()
        {
            if (Show)
            {
                var originalSkin = GUI.skin;
                GUI.skin = InterfaceMaker.CustomSkin;

                if (_obsoleteCursor)
                    _curLockState.SetValue(null, false, null);
                else
                    _curLockState.SetValue(null, 0, null);
                
                _curVisible.SetValue(null, true, null);
                
                Inspector.DisplayInspector();
                TreeViewer.DisplayViewer();
                Repl?.DisplayWindow();
                
                MouseInspect.OnGUI();

                // Restore old skin for maximum compatibility
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
                        _previousCursorLockState = _obsoleteCursor ? Convert.ToInt32((bool)_curLockState.GetValue(null, null)) : (int)_curLockState.GetValue(null, null);
                        _previousCursorVisible = (bool)_curVisible.GetValue(null, null);
                    }
                    else
                    {
                        if (!_previousCursorVisible || _previousCursorLockState != 0)
                        {
                            if(_obsoleteCursor)
                                _curLockState.SetValue(null, Convert.ToBoolean(_previousCursorLockState), null);
                            else
                                _curLockState.SetValue(null, _previousCursorLockState, null);
                
                            _curVisible.SetValue(null, _previousCursorVisible, null);
                        }
                    }
                }

                TreeViewer.Enabled = value;

                if (_gizmoDrawer != null)
                {
                    _gizmoDrawer.Show = value;
                    _gizmoDrawer.UpdateState(TreeViewer.SelectedTransform);
                }

                if (value)
                {
                    SetWindowSizes();

                    RefreshGameObjectSearcher(true);
                }
            }
        }

        internal void Update()
        {
            if (Input.GetKeyDown(ShowHotkey))
                Show = !Show;

            if (Show)
            {
                RefreshGameObjectSearcher(false);

                if (_obsoleteCursor)
                    _curLockState.SetValue(null, false, null);
                else
                    _curLockState.SetValue(null, 0, null);
                
                _curVisible.SetValue(null, true, null);

                TreeViewer.Update();

                MouseInspect.Update();
            }
        }

        internal void LateUpdate()
        {
            if (Show)
            {
                if (_obsoleteCursor)
                    _curLockState.SetValue(null, false, null);
                else
                    _curLockState.SetValue(null, 0, null);
                
                _curVisible.SetValue(null, true, null);
            }
        }

        private void RefreshGameObjectSearcher(bool full)
        {
            bool GizmoFilter(GameObject o) => o.name.StartsWith(GizmoDrawer.GizmoObjectName);
            var gizmosExist = _gizmoDrawer != null && _gizmoDrawer.Lines.Count > 0;
            _gameObjectSearcher.Refresh(full, gizmosExist ? GizmoFilter : (Predicate<GameObject>)null);
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
