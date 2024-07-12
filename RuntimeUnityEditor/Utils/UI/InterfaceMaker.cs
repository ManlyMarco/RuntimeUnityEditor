using System;
using System.Reflection;
using BepInEx;
using RuntimeUnityEditor.Core.Utils;
using RuntimeUnityEditor.Core.Utils.Abstractions;
using UnityEngine;
using UnityEngine.InputSystem;
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
        private static InitSettings.Setting<int> _fontSize;
        private static InitSettings.Setting<string> _fontName;

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
            _fontSize = RuntimeUnityEditorCore._initSettings.RegisterSetting("General", "font Size", 14, "Controls font size on tool's UI.");
            _fontName = RuntimeUnityEditorCore._initSettings.RegisterSetting("General", "font Name", "Arial.ttf", "Controls font used on tool's UI, should be dynamic if used with size.");
            
            UnityEngine.Font _font = Resources.GetBuiltinResource(typeof(Font), _fontName.Value) as Font;
            // Load the custom skin from resources
            var _boxBackgroundFile = RuntimeUnityEditorCore._initSettings.RegisterSetting("Skin", "Box Background", "guisharp-box.png", "Controls Resource Asset Background filename for Box UI Components.");
            var texData = ResourceUtils.GetEmbeddedResource(_boxBackgroundFile.Value);
            _boxBackground = UnityFeatureHelper.LoadTexture(texData);
            Object.DontDestroyOnLoad(_boxBackground);
            newSkin.box.onNormal.background = null;
            newSkin.box.normal.background = _boxBackground;
            var boxTextColor = RuntimeUnityEditorCore._initSettings.RegisterSetting("Skin", "Box Text Color", "255,255,255,1", "RGBA Value that Controls Text Color for Box UI Components.");
            string[] boxTextColorValues = boxTextColor.Value.Split(',');
            newSkin.box.normal.textColor = new Color(float.Parse(boxTextColorValues[0]),float.Parse(boxTextColorValues[1]),float.Parse(boxTextColorValues[2]),float.Parse(boxTextColorValues[3]));
            var _windowBackgroundFile = RuntimeUnityEditorCore._initSettings.RegisterSetting("Skin", "Window Background", "guisharp-window.png", "Controls Resource Asset Background filename for Window UI Components.");
            texData = ResourceUtils.GetEmbeddedResource(_windowBackgroundFile.Value);
            _winBackground = UnityFeatureHelper.LoadTexture(texData);
            Object.DontDestroyOnLoad(_winBackground);
            newSkin.window.onNormal.background = null;
            newSkin.window.normal.background = _winBackground;
            var windowPadding = RuntimeUnityEditorCore._initSettings.RegisterSetting("Skin", "Window Padding", "6,6,22,6", "Int Values that controls padding for window UI Components.");
            string[] windowPaddings = windowPadding.Value.Split(',');
            newSkin.window.padding = new RectOffset(int.Parse(windowPaddings[0]), int.Parse(windowPaddings[1]), int.Parse(windowPaddings[2]), int.Parse(windowPaddings[3]));
            var windowBorder = RuntimeUnityEditorCore._initSettings.RegisterSetting("Skin", "Window Border", "10,10,20,10", "Int Values that controls border for window UI Components.");
            string[] windowBorders = windowBorder.Value.Split(',');
            newSkin.window.border = new RectOffset(int.Parse(windowBorders[0]), int.Parse(windowBorders[1]), int.Parse(windowBorders[2]), int.Parse(windowBorders[3]));

            var windowTextColor = RuntimeUnityEditorCore._initSettings.RegisterSetting("Skin", "Window Text Color", "255,255,255,1", "RGBA Value that Controls Text Color for Window UI Components.");
            string[] windowTextColorValues = windowTextColor.Value.Split(',');
            newSkin.window.normal.textColor = new Color(float.Parse(windowTextColorValues[0]), float.Parse(windowTextColorValues[1]), float.Parse(windowTextColorValues[2]), float.Parse(windowTextColorValues[3]));
            var labelTextColor = RuntimeUnityEditorCore._initSettings.RegisterSetting("Skin", "Label Text Color", "255,255,255,1", "RGBA Value that Controls Text Color for Label UI Components.");
            string[] labelTextColorValues = labelTextColor.Value.Split(',');
            newSkin.label.normal.textColor = new Color(float.Parse(labelTextColorValues[0]), float.Parse(labelTextColorValues[1]), float.Parse(labelTextColorValues[2]), float.Parse(labelTextColorValues[3]));

            var buttonPadding = RuntimeUnityEditorCore._initSettings.RegisterSetting("Skin", "Button Padding", "4,4,3,3", "Int Values that controls padding for button UI Components.");
            string[] buttonPaddings = buttonPadding.Value.Split(',');
            newSkin.button.padding = new RectOffset(int.Parse(buttonPaddings[0]), int.Parse(buttonPaddings[1]), int.Parse(buttonPaddings[2]), int.Parse(buttonPaddings[3]));
            var buttonTextColor = RuntimeUnityEditorCore._initSettings.RegisterSetting("Skin", "Button Text Color", "255,255,255,1", "RGBA Value that Controls Text Color for button UI Components.");
            string[] buttonTextColorValues = buttonTextColor.Value.Split(',');
            newSkin.button.normal.textColor = new Color(float.Parse(buttonTextColorValues[0]), float.Parse(buttonTextColorValues[1]), float.Parse(buttonTextColorValues[2]), float.Parse(buttonTextColorValues[3]));
            var textFieldTextColor = RuntimeUnityEditorCore._initSettings.RegisterSetting("Skin", "TextField Text Color", "255,255,255,1", "RGBA Value that Controls Text Color for textField UI Components.");
            string[] textFieldTextColorValues = buttonTextColor.Value.Split(',');
            newSkin.textField.normal.textColor = new Color(float.Parse(textFieldTextColorValues[0]), float.Parse(textFieldTextColorValues[1]), float.Parse(textFieldTextColorValues[2]), float.Parse(textFieldTextColorValues[3]));

            newSkin.font = _font;
            newSkin.box.fontSize = _fontSize.Value;
            newSkin.toggle.fontSize = _fontSize.Value;
            newSkin.button.fontSize = _fontSize.Value;
            newSkin.textField.fontSize = _fontSize.Value;
            newSkin.label.fontSize = _fontSize.Value;
            newSkin.textArea.fontSize = _fontSize.Value;
            newSkin.window.fontSize = _fontSize.Value;
            newSkin.verticalScrollbar.fontSize = _fontSize.Value;
            newSkin.horizontalScrollbar.fontSize = _fontSize.Value;
            newSkin.verticalScrollbarDownButton.fontSize = _fontSize.Value;
            newSkin.verticalScrollbarThumb.fontSize = _fontSize.Value;
            newSkin.verticalScrollbarUpButton.fontSize = _fontSize.Value;
            newSkin.horizontalScrollbarLeftButton.fontSize = _fontSize.Value;
            newSkin.horizontalScrollbarRightButton.fontSize = _fontSize.Value;
            newSkin.horizontalScrollbarThumb.fontSize = _fontSize.Value;
            newSkin.horizontalSlider.fontSize = _fontSize.Value;
            newSkin.horizontalSliderThumb.fontSize = _fontSize.Value;

            var stretchHeight = RuntimeUnityEditorCore._initSettings.RegisterSetting("Skin", "Stretch Height", "false", "Controls stretching heights for UI Components.");
            var boolStretchHeight = bool.Parse(stretchHeight.Value);
            newSkin.button.stretchHeight = boolStretchHeight;
            newSkin.label.stretchHeight = boolStretchHeight;
            newSkin.textArea.stretchHeight = boolStretchHeight;
            newSkin.textField.stretchHeight = boolStretchHeight;
            newSkin.window.stretchHeight = boolStretchHeight;
            newSkin.box.stretchHeight = boolStretchHeight;
            newSkin.toggle.stretchHeight = boolStretchHeight;

            var stretchWidth = RuntimeUnityEditorCore._initSettings.RegisterSetting("Skin", "Stretch Width", "false", "Controls stretching widths for UI Components.");
            var boolStretchWidth = bool.Parse(stretchWidth.Value);
            newSkin.textField.stretchWidth = boolStretchWidth;
            newSkin.window.stretchWidth = boolStretchWidth;
            newSkin.box.stretchWidth = boolStretchWidth;
            newSkin.button.stretchWidth = boolStretchWidth;
            newSkin.textArea.stretchWidth = boolStretchWidth;
            newSkin.toggle.stretchWidth = boolStretchWidth;
            newSkin.label.stretchWidth = boolStretchWidth;

            return newSkin;
        }
    }
}
