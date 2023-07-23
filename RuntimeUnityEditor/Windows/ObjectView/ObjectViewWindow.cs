using System;
using System.Collections.Generic;
using System.Linq;
using HarmonyLib;
using RuntimeUnityEditor.Core.Utils;
using RuntimeUnityEditor.Core.Utils.Abstractions;
using UnityEngine;

namespace RuntimeUnityEditor.Core.ObjectView
{
    public sealed class ObjectViewWindow : Window<ObjectViewWindow>
    {
        private object _objToDisplay;
        private Action<object> _objDrawer;

        private Vector2 _scrollPos;

        private static Dictionary<Type, Action<object>> _objectDrawers = new Dictionary<Type, Action<object>>
        {
            {typeof(Texture), o => GUILayout.Label((Texture)o, GUILayout.ExpandWidth(true), GUILayout.ExpandHeight(true))},
            {typeof(GUIContent), o => GUILayout.Label((GUIContent)o, GUILayout.ExpandWidth(true), GUILayout.ExpandHeight(true))},
            {typeof(string), o => GUILayout.TextArea((string)o, GUILayout.ExpandWidth(true), GUILayout.ExpandHeight(true))},
        };

        private static bool GetDrawer(object objToDisplay, out Action<object> drawer)
        {
            if (objToDisplay == null)
            {
                drawer = o => GUILayout.Label("No object selected.\n\nYou can send objects here from other windows by clicking the \"Preview\" or \"V\" buttons.");
                return false;
            }

            if (objToDisplay is UnityEngine.Object uo && !uo)
            {
                drawer = o => GUILayout.Label($"Selected Unity Object of type [{uo.GetType().GetFancyDescription()}] has been destroyed.");
                return false;
            }

            var objType = objToDisplay.GetType();
            if (_objectDrawers.TryGetValue(objType, out drawer))
                return true;
                
            drawer = _objectDrawers.FirstOrDefault(x => x.Key.IsAssignableFrom(objType)).Value;
            if (drawer != null)
                return true;

            drawer = o => GUILayout.Label($"Unsupported object type: {objToDisplay?.GetType()}");
            return false;
        }

        public bool CanPreview(object obj)
        {
            return GetDrawer(obj, out _);
        }

        public void SetShownObject(object objToDisplay, string objName)
        {
            _objToDisplay = objToDisplay;
            GetDrawer(objToDisplay, out _objDrawer);

            _scrollPos = Vector2.zero;

            Title = "Object viewer - " + (objName ?? objToDisplay?.GetType().FullDescription() ?? "NULL");

            Enabled = true;
        }

        protected override Rect GetDefaultWindowRect(Rect screenRect)
        {
            return MakeDefaultWindowRect(screenRect, TextAlignment.Left);
        }

        protected override void DrawContents()
        {
            if (_objDrawer == null) GetDrawer(_objToDisplay, out _objDrawer);

            GUILayout.BeginVertical();
            {
                GUILayout.BeginHorizontal();
                {
                    ContextMenu.Instance.DrawContextButton(_objToDisplay, null);
                }
                GUILayout.EndHorizontal();

                _scrollPos = GUILayout.BeginScrollView(_scrollPos, true, true, GUILayout.ExpandWidth(true), GUILayout.ExpandHeight(true));
                {
                    _objDrawer(_objToDisplay);
                }
                GUILayout.EndScrollView();
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