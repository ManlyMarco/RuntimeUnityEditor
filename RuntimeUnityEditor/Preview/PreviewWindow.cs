using RuntimeUnityEditor.Core.Utils;
using UnityEngine;

namespace RuntimeUnityEditor.Core.Preview
{
    public sealed class PreviewWindow : WindowBase<PreviewWindow>
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

            Title = "Object preview window - " + (objName ?? "NULL");
            
            Enabled = true;
        }

        protected override void DrawContents()
        {
            GUILayout.BeginVertical();
            {
                if (_objToDisplay == null)
                {
                    GUILayout.FlexibleSpace();
                    GUILayout.Label("No object selected or the object has been destroyed.");
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
    }
}