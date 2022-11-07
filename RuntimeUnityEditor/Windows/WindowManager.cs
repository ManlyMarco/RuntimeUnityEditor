using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
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

        protected override void Initialize(InitSettings initSettings)
        {
            DisplayType = FeatureDisplayType.Hidden;
            _windowId = GetHashCode();
        }

        public void SetFeatures(List<IFeature> initializedFeatures)
        {
            //var groups = initializedFeatures.ToLookup(x => x.DisplayType);
            //groups[FeatureDisplayType.Window]
            _orderedFeatures = initializedFeatures.OrderByDescending(x => x.DisplayType).ToList();
        }

        protected override void OnGUI()
        {
            //Screen.width
            _windowRect = GUILayout.Window(_windowId, _windowRect, DrawTaskbar, $"{RuntimeUnityEditorCore.GUID} v{RuntimeUnityEditorCore.Version} / Press {RuntimeUnityEditorCore.Instance.ShowHotkey} to show/hide", GUILayout.ExpandHeight(false), GUILayout.ExpandWidth(false));
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
                    if (GUILayout.Button(feature.GetType().Name))
                        feature.Enabled = !feature.Enabled;
                }
                else if (feature.DisplayType == FeatureDisplayType.Feature)
                {
                    GUI.color = Color.white;
                    feature.Enabled = GUILayout.Toggle(feature.Enabled, feature.GetType().Name);
                }
            }
            GUILayout.EndHorizontal();
        }
    }
}
