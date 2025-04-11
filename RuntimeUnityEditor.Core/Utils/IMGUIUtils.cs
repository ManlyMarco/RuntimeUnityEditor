﻿using RuntimeUnityEditor.Core.Utils.Abstractions;
using UnityEngine;

namespace RuntimeUnityEditor.Core.Utils
{
    /// <summary>
    /// Utility methods for working with IMGUI / OnGui.
    /// </summary>
    public static class IMGUIUtils
    {
        /// <summary>
        /// Options with GUILayout.ExpandWidth(true). Useful for avoiding allocations.
        /// </summary>
        public static readonly GUILayoutOption[] LayoutOptionsExpandWidthTrue = { GUILayout.ExpandWidth(true) };
        
        /// <summary>
        /// Options with GUILayout.ExpandWidth(false). Useful for avoiding allocations.
        /// </summary>
        public static readonly GUILayoutOption[] LayoutOptionsExpandWidthFalse = { GUILayout.ExpandWidth(false) };

        private static Texture2D SolidBoxTex { get; set; }

        /// <summary>
        /// Draw a gray non-transparent GUI.Box at the specified rect. Use before a GUI.Window or other controls to get rid of 
        /// the default transparency and make the GUI easier to read.
        /// <example>
        /// IMGUIUtils.DrawSolidBox(screenRect);
        /// GUILayout.Window(362, screenRect, TreeWindow, "Select character folder");
        /// </example>
        /// </summary>
        public static void DrawSolidBox(Rect boxRect)
        {
            if (SolidBoxTex == null)
            {
                var windowBackground = new Texture2D(1, 1, TextureFormat.ARGB32, false);
#if KK || EC // Take the color correction filter into account
                windowBackground.SetPixel(0, 0, new Color(0.84f, 0.84f, 0.84f));
#else
                windowBackground.SetPixel(0, 0, new Color(0.4f, 0.4f, 0.4f));
#endif
                windowBackground.Apply();
                SolidBoxTex = windowBackground;
            }

            // It's necessary to make a new GUIStyle here or the texture doesn't show up
            GUI.Box(boxRect, GUIContent.none, new GUIStyle { normal = new GUIStyleState { background = SolidBoxTex } });
        }

        /// <summary>
        /// Block input from going through to the game/canvases if the mouse cursor is within the specified Rect.
        /// Use after a GUI.Window call or the window will not be able to get the inputs either.
        /// <example>
        /// GUILayout.Window(362, screenRect, TreeWindow, "Select character folder");
        /// Utils.EatInputInRect(screenRect);
        /// </example>
        /// </summary>
        /// <param name="eatRect"></param>
        public static void EatInputInRect(Rect eatRect)
        {
            var mousePos = UnityInput.Current.mousePosition;
            if (eatRect.Contains(new Vector2(mousePos.x, Screen.height - mousePos.y)))
                UnityInput.Current.ResetInputAxes();
        }

        /// <summary>
        /// Draw a label with an outline
        /// </summary>
        /// <param name="rect">Size of the control</param>
        /// <param name="text">Text of the label</param>
        /// <param name="style">Style to be applied to the label</param>
        /// <param name="txtColor">Color of the text</param>
        /// <param name="outlineColor">Color of the outline</param>
        /// <param name="outlineThickness">Thickness of the outline in pixels</param>
        public static void DrawLabelWithOutline(Rect rect, string text, GUIStyle style, Color txtColor, Color outlineColor, int outlineThickness)
        {
            var backupColor = style.normal.textColor;
            var backupGuiColor = GUI.color;

            style.normal.textColor = outlineColor;
            GUI.color = outlineColor;

            var baseRect = rect;

            rect.x -= outlineThickness;
            rect.y -= outlineThickness;

            while (rect.x++ < baseRect.x + outlineThickness)
                GUI.Label(rect, text, style);
            rect.x--;

            while (rect.y++ < baseRect.y + outlineThickness)
                GUI.Label(rect, text, style);
            rect.y--;

            while (rect.x-- > baseRect.x - outlineThickness)
                GUI.Label(rect, text, style);
            rect.x++;

            while (rect.y-- > baseRect.y - outlineThickness)
                GUI.Label(rect, text, style);

            style.normal.textColor = txtColor;
            GUI.color = txtColor;

            GUI.Label(baseRect, text, style);

            style.normal.textColor = backupColor;
            GUI.color = backupGuiColor;
        }

        /// <summary>
        /// Draw a label with a shadow
        /// </summary>        
        /// <param name="rect">Size of the control</param>
        /// <param name="content">Contents of the label</param>
        /// <param name="style">Style to be applied to the label</param>
        /// <param name="txtColor">Color of the outline</param>
        /// <param name="shadowColor">Color of the text</param>
        /// <param name="shadowOffset">Offset of the shadow in pixels</param>
        public static void DrawLabelWithShadow(Rect rect, GUIContent content, GUIStyle style, Color txtColor, Color shadowColor, Vector2 shadowOffset)
        {
            var backupColor = style.normal.textColor;

            style.normal.textColor = shadowColor;
            rect.x += shadowOffset.x;
            rect.y += shadowOffset.y;
            GUI.Label(rect, content, style);

            style.normal.textColor = txtColor;
            rect.x -= shadowOffset.x;
            rect.y -= shadowOffset.y;
            GUI.Label(rect, content, style);

            style.normal.textColor = backupColor;
        }

        /// <summary>
        /// Draw a label with a shadow
        /// </summary>
        public static void DrawLayoutLabelWithShadow(GUIContent content, GUIStyle style, Color txtColor, Color shadowColor, Vector2 direction, params GUILayoutOption[] options)
        {
            DrawLabelWithShadow(GUILayoutUtility.GetRect(content, style, options), content, style, txtColor, shadowColor, direction);
        }

        /// <summary>
        /// Draw a button with a shadow
        /// </summary>
        public static bool DrawButtonWithShadow(Rect r, GUIContent content, GUIStyle style, float shadowAlpha, Vector2 direction)
        {
            GUIStyle letters = style.CreateCopy();
            letters.normal.background = null;
            letters.hover.background = null;
            letters.active.background = null;

            bool result = GUI.Button(r, content, style);

            Color color = r.Contains(Event.current.mousePosition) ? letters.hover.textColor : letters.normal.textColor;

            DrawLabelWithShadow(r, content, letters, color, new Color(0f, 0f, 0f, shadowAlpha), direction);

            return result;
        }
        
        /// <summary>
        /// Draw a button with a shadow
        /// </summary>
        public static bool DrawLayoutButtonWithShadow(GUIContent content, GUIStyle style, float shadowAlpha, Vector2 direction, params GUILayoutOption[] options)
        {
            return DrawButtonWithShadow(GUILayoutUtility.GetRect(content, style, options), content, style, shadowAlpha, direction);
        }

        #region Resizing

        private static bool _handleClicked;
        private static Vector3 _clickedPosition;
        private static Rect _originalWindow;
        private static int _currentWindowId;

        /// <summary>
        /// Handle dragging and resizing of ongui windows, and preventing inptus from going through. Use instead of GUI.DragWindow()
        /// How to use: _winRect = Utils.DragResizeEat(id, _winRect);
        /// </summary>
        public static Rect DragResizeEat(int id, Rect rect)
        {
            var result = DragOrResize(id, rect);
            EatInputInRect(result);
            return result;
        }

        /// <summary>
        /// Handle both dragging and resizing of ongui windows. Use instead of GUI.DragWindow()
        /// How to use: _winRect = Utils.DragOrResize(id, _winRect);
        /// </summary>
        public static Rect DragOrResize(int id, Rect rect)
        {
            const int visibleAreaSize = 10;
            GUI.Box(new Rect(rect.width - visibleAreaSize - 2, rect.height - visibleAreaSize - 2, visibleAreaSize, visibleAreaSize), GUIContent.none);

            if (_currentWindowId != 0 && _currentWindowId != id) return rect;

            var mousePos = UnityInput.Current.mousePosition;
            mousePos.y = Screen.height - mousePos.y; // Convert to GUI coords

            var winRect = rect;
            const int functionalAreaSize = 25;
            var windowHandle = new Rect(
                winRect.x + winRect.width - functionalAreaSize,
                winRect.y + winRect.height - functionalAreaSize,
                functionalAreaSize,
                functionalAreaSize);

            // Can't use Input class because we eat inputs
            var mouseButtonDown = Event.current.isMouse &&
                                  Event.current.type == EventType.MouseDown &&
                                  Event.current.button == 0;
            if (mouseButtonDown && windowHandle.Contains(mousePos))
            {
                _handleClicked = true;
                _clickedPosition = mousePos;
                _originalWindow = winRect;
                _currentWindowId = id;
            }

            if (_handleClicked)
            {
                // Resize window by dragging
                //if (UnityInput.Current.GetMouseButton(0))
                {
                    var listWinRect = winRect;
                    listWinRect.width = Mathf.Clamp(_originalWindow.width + (mousePos.x - _clickedPosition.x), 100, Screen.width);
                    listWinRect.height =
                        Mathf.Clamp(_originalWindow.height + (mousePos.y - _clickedPosition.y), 100, Screen.height);
                    rect = listWinRect;
                }

                var mouseButtonUp = Event.current.isMouse &&
                                    Event.current.type == EventType.MouseUp &&
                                    Event.current.button == 0;
                if (mouseButtonUp)
                {
                    _handleClicked = false;
                    _currentWindowId = 0;
                }
            }
            else
            {
                GUI.DragWindow();
            }
            return rect;
        }

        #endregion

        /// <summary>
        /// For use inside OnGUI to check if a GUI.Button was clicked with middle mouse button.
        /// </summary>
        public static bool IsMouseWheelClick()
        {
            return Event.current.button == 2;
        }
        
        /// <summary>
        /// For use inside OnGUI to check if a GUI.Button was clicked with right mouse button.
        /// </summary>
        public static bool IsMouseRightClick()
        {
            return Event.current.button == 1;
        }

        /// <summary>
        /// Use instead of new GUIStyle(original) for compat with IL2CPP.
        /// </summary>
        public static GUIStyle CreateCopy(this GUIStyle original)
        {
#if IL2CPP
            // Copy constructor is sometimes stripped out in IL2CPP
            var guiStyle = new GUIStyle();
            guiStyle.m_Ptr = GUIStyle.Internal_Copy(guiStyle, original);
            return guiStyle;
#else
            return new GUIStyle(original);
#endif
        }
    }
}
