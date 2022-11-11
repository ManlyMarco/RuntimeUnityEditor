using RuntimeUnityEditor.Core.Utils;
using RuntimeUnityEditor.Core.Utils.Abstractions;
using UnityEngine;

namespace RuntimeUnityEditor.Core.ObjectView
{
    public sealed class ObjectViewWindow : Window<ObjectViewWindow>
    {
        private object _objToDisplay;
        private Vector2 _scrollPos;

        public void SetShownObject(object objToDisplay, string objName)
        {
            // todo make more generic and support more types
            if (objToDisplay == null || objToDisplay is Texture || objToDisplay is string)
                _objToDisplay = objToDisplay;
            else
                _objToDisplay = $"Unsupported object type: {objToDisplay.GetType()}\n{new System.Diagnostics.StackTrace()}";

            Title = "Object viewer - " + (objName ?? "NULL");

            Enabled = true;
        }

        protected override Rect GetDefaultWindowRect(Rect screenRect)
        {
            return new Rect(screenRect.xMin, screenRect.yMin, SideWidth, SideWidth);
        }

        protected override void DrawContents()
        {
            GUILayout.BeginVertical();
            {
                if (_objToDisplay == null)
                {
                    GUILayout.FlexibleSpace();
                    GUILayout.Label("No object selected or the object has been destroyed.\n\nYou can send objects here from other windows by clicking the \"View\" or \"V\" buttons.");
                    GUILayout.FlexibleSpace();
                }
                else
                {
                    _scrollPos = GUILayout.BeginScrollView(_scrollPos, true, true, GUILayout.ExpandWidth(true), GUILayout.ExpandHeight(true));
                    // todo make more generic and support more types
                    {
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
            }
            GUILayout.EndVertical();

        }

        protected override void Initialize(InitSettings initSettings)
        {
            Title = "Object viewer - Empty";
            DisplayName = "Viewer";
        }
    }
}