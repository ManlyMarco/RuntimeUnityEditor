using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using RuntimeUnityEditor.Core.Utils;
using RuntimeUnityEditor.Core.Utils.Abstractions;
using UnityEngine;

namespace RuntimeUnityEditor.Core
{
    /// <summary>
    /// Taskbar with controls for all of the RUE features.
    /// Avoid using this class directly, new features can be added with <see cref="RuntimeUnityEditorCore.AddFeature"/> instead.
    /// </summary>
    public class Taskbar : IFeature
    {
        private int _windowId;
        private Rect _windowRect;

        private readonly GUILayoutOption[] _taskbarLayoutOptions = { GUILayoutShim.MaxWidth(Screen.width), GUILayoutShim.ExpandHeight(false), GUILayoutShim.ExpandWidth(false) };
        private readonly GUILayoutOption[] _timescaleTextfieldOptions = { GUILayout.Width(38) };

        private List<IFeature> _orderedFeatures;
        private string _title;
        private string _titleFull;
        private bool _enabled;

        /// <summary>
        /// Current instance.
        /// </summary>
        public static Taskbar Instance { get; private set; }

        /// <summary>
        /// Height of the taskbar.
        /// </summary>
        public int Height => (int)_windowRect.height;

        /// <summary>
        /// Do not create additional instances or things will break.
        /// This has to be public or things will break.
        /// </summary>
        public Taskbar()
        {
            Instance = this;
        }

        void IFeature.OnInitialize(InitSettings initSettings)
        {
            _windowId = GetHashCode();
            _title = $"{RuntimeUnityEditorCore.GUID} v{RuntimeUnityEditorCore.Version}";
        }

        /// <summary>
        /// Set features shown in the taskbar.
        /// </summary>
        public void SetFeatures(List<IFeature> initializedFeatures)
        {
            _orderedFeatures = initializedFeatures.OrderByDescending(x => x.DisplayType).ThenBy(x => x.DisplayName).ToList();
        }

        void IFeature.OnOnGUI()
        {
            _windowRect = GUILayout.Window(
                _windowId,
                _windowRect,
                (GUI.WindowFunction)DrawTaskbar,
                _titleFull,
                _taskbarLayoutOptions
            );
            IMGUIUtils.EatInputInRect(_windowRect);
            _windowRect.x = (int)((Screen.width - _windowRect.width) / 2);
            _windowRect.y = (int)(Screen.height - _windowRect.height);
        }

        private void DrawTaskbar(int id)
        {
            GUILayout.BeginHorizontal(IMGUIUtils.EmptyLayoutOptions);

            var firstFeature = true;
            for (var i = 0; i < _orderedFeatures.Count; i++)
            {
                var feature = _orderedFeatures[i];
                if (feature.DisplayType == FeatureDisplayType.Window)
                {
                    GUI.color = feature.Enabled ? Color.cyan : Color.white;
                    if (GUILayout.Button(feature.DisplayName, IMGUIUtils.EmptyLayoutOptions))
                        feature.Enabled = !feature.Enabled;
                }
                else if (feature.DisplayType == FeatureDisplayType.Feature)
                {
                    if (firstFeature)
                    {
                        GUI.color = new Color(1f, 1f, 1f, 0.75f);
                        if (GUILayout.Button("Reset", IMGUIUtils.EmptyLayoutOptions))
                        {
                            // Needed to avoid WindowManager.ResetWindowRect using old rects as reference to where new windows should be placed.
                            foreach (var window in _orderedFeatures.OfType<IWindow>())
                                window.WindowRect = new Rect(-1000, -1000, 0, 0);

                            foreach (var window in _orderedFeatures.OfType<IWindow>().OrderByDescending(x => x.Enabled).ThenBy(x => x.Title))
                            {
                                // Ensure that all title bars are visible
                                GUI.BringWindowToFront(window.WindowId);
                                WindowManager.ResetWindowRect(window);
                            }
                        }

                        firstFeature = false;
                        GUI.color = Color.white;
                        GUILayout.Label("|", IMGUIUtils.EmptyLayoutOptions);
                    }

                    feature.Enabled = GUILayout.Toggle(feature.Enabled, feature.DisplayName, IMGUIUtils.EmptyLayoutOptions);
                }
            }

            GUILayout.Label("|", IMGUIUtils.EmptyLayoutOptions);

            GUILayout.Label("Time", IMGUIUtils.LayoutOptionsExpandWidthFalse);

            if (GUILayout.Button(">", IMGUIUtils.LayoutOptionsExpandWidthFalse))
                Time.timeScale = 1;
            if (GUILayout.Button("||", IMGUIUtils.LayoutOptionsExpandWidthFalse))
                Time.timeScale = 0;

            if (float.TryParse(GUILayout.TextField(Time.timeScale.ToString("F2", CultureInfo.InvariantCulture), _timescaleTextfieldOptions), NumberStyles.Any, CultureInfo.InvariantCulture, out var newVal))
                Time.timeScale = newVal;

            GUI.changed = false;
            var n = GUILayout.Toggle(Application.runInBackground, "in BG", IMGUIUtils.EmptyLayoutOptions);
            if (GUI.changed) Application.runInBackground = n;

            GUILayout.Label("|", IMGUIUtils.EmptyLayoutOptions);

            if (GUILayout.Button("Log", IMGUIUtils.LayoutOptionsExpandWidthFalse))
                UnityFeatureHelper.OpenLog();

            AssetBundleManagerHelper.DrawButtonIfAvailable();

            GUILayout.EndHorizontal();
        }

        /// <summary>
        /// Show the taskbar, and by extension entire RUE interface. Use <see cref="RuntimeUnityEditorCore.Show"/> instead.
        /// </summary>
        public bool Enabled
        {
            get => _enabled;
            set
            {
                if (value)
                {
                    _taskbarLayoutOptions[0] = GUILayoutShim.MaxWidth(Screen.width);
                    _titleFull = RuntimeUnityEditorCore.Instance.ShowHotkey == KeyCode.None ? _title : $"{_title} / Press {RuntimeUnityEditorCore.Instance.ShowHotkey} to show/hide";
                }
                _enabled = value;
            }
        }

        void IFeature.OnUpdate() { }
        void IFeature.OnLateUpdate() { }
        void IFeature.OnEditorShownChanged(bool visible) { }
        FeatureDisplayType IFeature.DisplayType => FeatureDisplayType.Hidden;
        string IFeature.DisplayName => "WindowManager";
    }
}
