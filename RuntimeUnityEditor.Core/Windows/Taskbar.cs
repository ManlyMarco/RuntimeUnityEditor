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
        private List<IFeature> _orderedFeatures;
        private string _title;

        /// <summary>
        /// Current instance.
        /// </summary>
        public static Taskbar Instance { get; private set; }

        /// <summary>
        /// Text shown in the title bar of the taskbar.
        /// </summary>
        protected string GetTitle() => RuntimeUnityEditorCore.Instance.ShowHotkey == KeyCode.None ? _title : _title + $" / Press {RuntimeUnityEditorCore.Instance.ShowHotkey} to show/hide";

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
                GetTitle(),
                GUILayout.ExpandHeight(false),
                GUILayout.ExpandWidth(false),
                GUILayoutShim.MaxWidth(Screen.width)
            );
            IMGUIUtils.EatInputInRect(_windowRect);
            _windowRect.x = (int)((Screen.width - _windowRect.width) / 2);
            _windowRect.y = (int)(Screen.height - _windowRect.height);
        }

        private void DrawTaskbar(int id)
        {
            GUILayout.BeginHorizontal();

            var firstFeature = true;
            foreach (var feature in _orderedFeatures)
            {
                if (feature.DisplayType == FeatureDisplayType.Window)
                {
                    GUI.color = feature.Enabled ? Color.cyan : Color.white;
                    if (GUILayout.Button(feature.DisplayName))
                        feature.Enabled = !feature.Enabled;
                }
                else if (feature.DisplayType == FeatureDisplayType.Feature)
                {
                    if (firstFeature)
                    {
                        GUI.color = new Color(1f, 1f, 1f, 0.75f);
                        if (GUILayout.Button("Reset"))
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
                        GUILayout.Label("|");
                    }
                    feature.Enabled = GUILayout.Toggle(feature.Enabled, feature.DisplayName);
                }
            }

            GUILayout.Label("|");

            GUILayout.Label("Time", IMGUIUtils.LayoutOptionsExpandWidthFalse);

            if (GUILayout.Button(">", IMGUIUtils.LayoutOptionsExpandWidthFalse))
                Time.timeScale = 1;
            if (GUILayout.Button("||", IMGUIUtils.LayoutOptionsExpandWidthFalse))
                Time.timeScale = 0;

            if (float.TryParse(GUILayout.TextField(Time.timeScale.ToString("F2", CultureInfo.InvariantCulture), GUILayout.Width(38)), NumberStyles.Any, CultureInfo.InvariantCulture, out var newVal))
                Time.timeScale = newVal;

            GUI.changed = false;
            var n = GUILayout.Toggle(Application.runInBackground, "in BG");
            if (GUI.changed) Application.runInBackground = n;

            GUILayout.Label("|");

            if (GUILayout.Button("Log", IMGUIUtils.LayoutOptionsExpandWidthFalse))
                UnityFeatureHelper.OpenLog();

            AssetBundleManagerHelper.DrawButtonIfAvailable();

            GUILayout.EndHorizontal();
        }

        /// <summary>
        /// Show the taskbar, and by extension entire RUE interface. Use <see cref="RuntimeUnityEditorCore.Show"/> instead.
        /// </summary>
        public bool Enabled { get; set; }
        void IFeature.OnUpdate() { }
        void IFeature.OnLateUpdate() { }
        void IFeature.OnEditorShownChanged(bool visible) { }
        FeatureDisplayType IFeature.DisplayType => FeatureDisplayType.Hidden;
        string IFeature.DisplayName => "WindowManager";
    }
}
