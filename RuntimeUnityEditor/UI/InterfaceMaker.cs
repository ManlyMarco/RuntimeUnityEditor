using System;
using RuntimeUnityEditor.Core.Utils;
using UnityEngine;
using Object = UnityEngine.Object;

namespace RuntimeUnityEditor.Core.UI
{
    public static class InterfaceMaker
    {
        // These all need to be held as static properties, including textures, to prevent UnloadUnusedAssets from destroying them
        private static Texture2D _boxBackground;
        private static Texture2D _winBackground;
        private static GUISkin _customSkin;

        public static void EatInputInRect(Rect eatRect)
        {
            if (eatRect.Contains(new Vector2(Input.mousePosition.x, Screen.height - Input.mousePosition.y)))
                Input.ResetInputAxes();
        }

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
            var newSkin = Object.Instantiate(GUI.skin);
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

            return newSkin;
        }
    }
}
