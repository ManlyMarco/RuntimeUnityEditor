using RuntimeUnityEditor.Core.UI;
using RuntimeUnityEditor.Core.Utils;
using UnityEngine;

namespace RuntimeUnityEditor.Core.Preview
{
    public sealed class PreviewWindow
    {
        private readonly int _windowId;
        private object _objToDisplay;
        private Rect _windowRect;
        private Vector2 _scrollPos;
        private string _windowTitle;

        public PreviewWindow()
        {
            _windowRect = new Rect(100, 100, 500, 500);
            _windowId = GetHashCode();
        }

        public void UpdateWindowSize(Rect rect)
        {
            _windowRect = rect;
        }

        public void SetShownObject(object objToDisplay, string objName)
        {
            // todo make more generic and support more types
            if (objToDisplay == null || objToDisplay is Texture || objToDisplay is string)
                _objToDisplay = objToDisplay;
            else
                _objToDisplay = $"Unsupported object type: {objToDisplay.GetType()}\n{new System.Diagnostics.StackTrace()}";

            _windowTitle = "Object preview window - " + (objName ?? "NULL");
        }

        public void DisplayWindow()
        {
            if (_objToDisplay == null) return;

            _windowRect = GUILayout.Window(_windowId, _windowRect, PreviewWindowFunc, _windowTitle);
            InterfaceMaker.EatInputInRect(_windowRect);
        }

        private void PreviewWindowFunc(int id)
        {
            GUILayout.BeginVertical();
            {
                if (GUILayout.Button("Close"))
                {
                    _objToDisplay = null;
                    return;
                }

                _scrollPos = GUILayout.BeginScrollView(_scrollPos, true, true, GUILayout.ExpandWidth(true), GUILayout.ExpandHeight(true));
                {
                    // todo make more generic and support more types
                    if (_objToDisplay is Texture tex)
                    {
                        if (GUILayout.Button("Save"))
                            tex.SaveTextureToFileWithDialog();

                        GUILayout.Label(tex, GUILayout.ExpandWidth(true), GUILayout.ExpandHeight(true));
                    }
                    else if (_objToDisplay is string str)
                    {
                        GUILayout.TextArea(str, GUILayout.ExpandWidth(true), GUILayout.ExpandHeight(true));
                    }
                }
                GUILayout.EndScrollView();
            }
            GUILayout.EndVertical();

            _windowRect = IMGUIUtils.DragOrResize(id, _windowRect);
        }
    }
}