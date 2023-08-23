using System;
using System.Reflection;
using RuntimeUnityEditor.Core.Utils;
using RuntimeUnityEditor.Core.Utils.Abstractions;
using UnityEngine;
using Object = UnityEngine.Object;

namespace RuntimeUnityEditor.Core.UI
{
    /// <summary>
    /// Handles the IMGUI skin and such.
    /// </summary>
    public static class InterfaceMaker
    {
        // These all need to be held as static properties, including textures, to prevent UnloadUnusedAssets from destroying them
        private static Texture2D _boxBackground;
        private static Texture2D _winBackground;
        private static GUISkin _customSkin;

        /// <summary>
        /// If mouse is inside of a given IMGUI screen rect, eat the input.
        /// </summary>
        public static void EatInputInRect(Rect eatRect)
        {
            var mousePos = UnityInput.Current.mousePosition;
            if (eatRect.Contains(new Vector2(mousePos.x, Screen.height - mousePos.y)))
                UnityInput.Current.ResetInputAxes();
        }

        /// <summary>
        /// IMGUI skin used by RUE. Can be used by other plugins to get the same look and feel, just don't modify its contents.
        /// </summary>
        public static GUISkin CustomSkin
        {
            get
            {
                if (_customSkin == null)
                {
                    try
                    {
                        _customSkin = CreateSkin();
                    }
                    catch (Exception ex)
                    {
                        RuntimeUnityEditorCore.Logger.Log(LogLevel.Warning, "Could not load custom GUISkin - " + ex.Message);
                        _customSkin = GUI.skin;
                    }
                }

                return _customSkin;
            }
        }

        private static GUISkin CreateSkin()
        {
            // Reflection because unity 4.x refuses to instantiate if built with newer versions of UnityEngine
            var newSkin = typeof(Object).GetMethod("Instantiate", BindingFlags.Static | BindingFlags.Public, null, new []{typeof(Object)}, null).Invoke(null, new object[] {GUI.skin}) as GUISkin;
            Object.DontDestroyOnLoad(newSkin);

            // Load the custom skin from resources
            var texData = ResourceUtils.GetEmbeddedResource("guisharp-box.png");
            _boxBackground = UnityFeatureHelper.LoadTexture(texData);
            Object.DontDestroyOnLoad(_boxBackground);
            newSkin.box.onNormal.background = null;
            newSkin.box.normal.background = _boxBackground;
            newSkin.box.normal.textColor = Color.white;

            texData = ResourceUtils.GetEmbeddedResource("guisharp-window.png");
            _winBackground = UnityFeatureHelper.LoadTexture(texData);
            Object.DontDestroyOnLoad(_winBackground);
            newSkin.window.onNormal.background = null;
            newSkin.window.normal.background = _winBackground;
            newSkin.window.padding = new RectOffset(6, 6, 22, 6);
            newSkin.window.border = new RectOffset(10, 10, 20, 10);
            newSkin.window.normal.textColor = Color.white;

            newSkin.button.padding = new RectOffset(4, 4, 3, 3);
            newSkin.button.normal.textColor = Color.white;

            newSkin.textField.normal.textColor = Color.white;

            newSkin.label.normal.textColor = Color.white;

            newSkin.toggle.stretchWidth = false;
            newSkin.label.stretchWidth = false;

            return newSkin;
        }
    }
}
