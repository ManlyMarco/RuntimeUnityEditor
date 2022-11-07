using System;
using RuntimeUnityEditor.Core.Utils;
using RuntimeUnityEditor.Core.Utils.Abstractions;
using UnityEngine;

namespace RuntimeUnityEditor.Core
{
    public abstract class Window<T> : FeatureBase<T> where T : Window<T>
    {
        protected const int ScreenOffset = 10;
        protected const int SideWidth = 350;

        private const int TooltipWidth = 400;
        // ReSharper disable StaticMemberInGenericType
        private static GUIStyle _tooltipStyle;
        private static GUIContent _tooltipContent;
        private static Texture2D _tooltipBackground;
        // ReSharper restore StaticMemberInGenericType

        protected Window()
        {
            WindowId = base.GetHashCode();
            DisplayType = FeatureDisplayType.Window;
        }

        public override string DisplayName
        {
            get => _displayName ?? (_displayName = Title ?? base.DisplayName);
            set => _displayName = value;
        }

        protected override void OnGUI()
        {
            //todo unnecessary, move to outer
            if (!Enabled) return;

            WindowRect = GUILayout.Window(WindowId, WindowRect, DrawContentsInt, Title);
            if (WindowRect.width < MinimumSize.x)
            {
                var rect = WindowRect;
                rect.width = MinimumSize.x;
                WindowRect = rect;
            }

            if (WindowRect.height < MinimumSize.y)
            {
                var rect = WindowRect;
                rect.height = MinimumSize.y;
                WindowRect = rect;
            }
        }

        private void DrawContentsInt(int id)
        {
            int visibleAreaSize = GUI.skin.window.border.top - 4;// 10;
            if (GUI.Button(new Rect(WindowRect.width - visibleAreaSize - 2, 2, visibleAreaSize, visibleAreaSize), "X"))
            {
                Enabled = false;
                return;
            }

            try
            {
                DrawContents();
                DrawTooltip(WindowRect);
            }
            catch (Exception ex)
            {
                // Ignore mismatch exceptions caused by virtual lists, there will be an unity error shown anyways
                if (!ex.Message.Contains("GUILayout"))
                    RuntimeUnityEditorCore.Logger.Log(LogLevel.Error, $"[{Title}] GUI crash: {ex}");
            }

            WindowRect = IMGUIUtils.DragResizeEat(id, WindowRect);
        }

        private static void DrawTooltip(Rect area)
        {
            if (!string.IsNullOrEmpty(GUI.tooltip))
            {
                if (_tooltipBackground == null)
                {
                    _tooltipBackground = new Texture2D(1, 1, TextureFormat.ARGB32, false);
                    _tooltipBackground.SetPixel(0, 0, Color.black);
                    _tooltipBackground.Apply();

                    _tooltipStyle = new GUIStyle
                    {
                        normal = new GUIStyleState { textColor = Color.white, background = _tooltipBackground },
                        wordWrap = true,
                        alignment = TextAnchor.MiddleCenter
                    };
                    _tooltipContent = new GUIContent();
                }

                _tooltipContent.text = GUI.tooltip;
                var height = _tooltipStyle.CalcHeight(_tooltipContent, TooltipWidth) + 10;

                var currentEvent = Event.current;

                var x = currentEvent.mousePosition.x + TooltipWidth > area.width
                    ? area.width - TooltipWidth
                    : currentEvent.mousePosition.x;

                var y = currentEvent.mousePosition.y + 25 + height > area.height
                    ? currentEvent.mousePosition.y - height
                    : currentEvent.mousePosition.y + 25;

                GUI.Box(new Rect(x, y, TooltipWidth, height), GUI.tooltip, _tooltipStyle);
            }
        }

        protected override void VisibleChanged(bool visible)
        {
            if (visible)
            {
                // todo generate once for all
                var screenRect = new Rect(
                    ScreenOffset,
                    ScreenOffset,
                    Screen.width - ScreenOffset * 2,
                    Screen.height - ScreenOffset * 2);
                WindowRect = GetDefaultWindowRect(screenRect);
            }
        }

        protected abstract Rect GetDefaultWindowRect(Rect screenRect);

        public static Rect GetCenterWindowDefaultRect(Rect screenRect)
        {
            var centerWidth = (int)Mathf.Min(850, screenRect.width);
            var centerX = (int)(screenRect.xMin + screenRect.width / 2 - Mathf.RoundToInt((float)centerWidth / 2));

            var inspectorHeight = (int)(screenRect.height / 4) * 3;
            return new Rect(centerX, screenRect.yMin, centerWidth, inspectorHeight);
        }

        protected abstract void DrawContents();

        public virtual string Title { get; set; }
        public int WindowId { get; set; }
        public virtual Rect WindowRect { get; set; }
        public Vector2 MinimumSize { get; set; } = new Vector2(100, 100);
    }
}
