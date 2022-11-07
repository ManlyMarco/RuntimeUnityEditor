﻿using System.Collections.Generic;
using System.Linq;
using RuntimeUnityEditor.Core.Utils;
using RuntimeUnityEditor.Core.Utils.Abstractions;
using UnityEngine;

namespace RuntimeUnityEditor.Core
{
    public class WindowManager : FeatureBase<WindowManager>
    {
        private int _windowId;
        private Rect _windowRect;
        private List<IFeature> _orderedFeatures;
        private string _title;

        protected string GetTitle() => RuntimeUnityEditorCore.Instance.ShowHotkey == KeyCode.None ? _title : _title + $" / Press {RuntimeUnityEditorCore.Instance.ShowHotkey} to show/hide";
        public int Height => (int)_windowRect.height;

        protected override void Initialize(InitSettings initSettings)
        {
            DisplayType = FeatureDisplayType.Hidden;
            _windowId = GetHashCode();
            _title = $"{RuntimeUnityEditorCore.GUID} v{RuntimeUnityEditorCore.Version}";
        }

        public void SetFeatures(List<IFeature> initializedFeatures)
        {
            //var groups = initializedFeatures.ToLookup(x => x.DisplayType);
            //groups[FeatureDisplayType.Window]
            _orderedFeatures = initializedFeatures.OrderByDescending(x => x.DisplayType).ToList();
        }

        protected override void OnGUI()
        {
            _windowRect = GUILayout.Window(_windowId, _windowRect, DrawTaskbar, GetTitle(), GUILayout.ExpandHeight(false), GUILayout.ExpandWidth(false), GUILayout.MaxWidth(Screen.width));
            IMGUIUtils.EatInputInRect(_windowRect);
            _windowRect.x = (int)((Screen.width - _windowRect.width) / 2);
            _windowRect.y = (int)(Screen.height - _windowRect.height);
        }

        private void DrawTaskbar(int id)
        {
            GUILayout.BeginHorizontal();
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
                    GUI.color = Color.white;
                    feature.Enabled = GUILayout.Toggle(feature.Enabled, feature.DisplayName);
                }
            }
            GUILayout.Space(5);
            if (GUILayout.Button("Reset"))
            {
                foreach (var window in _orderedFeatures.OfType<IWindow>())
                    window.ResetWindowRect();
            }
            GUILayout.EndHorizontal();
        }
    }
}