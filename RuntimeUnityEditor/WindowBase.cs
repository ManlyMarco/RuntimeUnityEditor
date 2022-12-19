using System;
using System.Collections;
using RuntimeUnityEditor.Core.Utils;
using RuntimeUnityEditor.Core.Utils.Abstractions;
using UnityEngine;

namespace RuntimeUnityEditor.Core
{
    public interface IWindow : IFeature
    {
        string Title { get; set; }
        int WindowId { get; set; }
        Rect WindowRect { get; set; }
        Vector2 MinimumSize { get; set; }
        void ResetWindowRect();
    }

    public abstract class Window<T> : FeatureBase<T>, IWindow where T : Window<T>
    {
        protected const int ScreenOffset = 10;
        protected const int SideWidth = 350;

        private const int TooltipWidth = 400;
        // ReSharper disable StaticMemberInGenericType
        private static GUIStyle _tooltipStyle;
        private static GUIContent _tooltipContent;
        private static Texture2D _tooltipBackground;
        // ReSharper restore StaticMemberInGenericType

        private bool _canShow;
        private Rect _windowRect;
        private Action<Rect> _confRect;

        protected Window()
        {
            DisplayType = FeatureDisplayType.Window;
            SettingCategory = "Windows";
        }

        protected override void AfterInitialized(InitSettings initSettings)
        {
            base.AfterInitialized(initSettings);
            WindowId = base.GetHashCode();
            _confRect = initSettings.RegisterSetting(SettingCategory, DisplayName + " window size", WindowRect, string.Empty, b => WindowRect = b);
        }

        public override string DisplayName
        {
            get => _displayName ?? (_displayName = Title ?? base.DisplayName);
            set => _displayName = value;
        }

        protected override void OnGUI()
        {
            if (!_canShow) return;

            var title = Title;
#if DEBUG
            title = $"{title} {WindowRect}";
#endif
            WindowRect = GUILayout.Window(WindowId, WindowRect, DrawContentsInt, title);
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

        protected override void OnVisibleChanged(bool visible)
        {
            // If the taskbar didn't have a chance to initialize yet, wait for a frame. Necessary to calculate free screen space.
            if (visible && WindowManager.Instance.Height == 0)
            {
                // todo more efficient way?
                IEnumerator DelayedVisible()
                {
                    yield return null;
                    base.OnVisibleChanged(Visible);
                }
                RuntimeUnityEditorCore.PluginObject.StartCoroutine(DelayedVisible());
            }
            else
            {
                base.OnVisibleChanged(visible);
            }
        }

        protected override void VisibleChanged(bool visible)
        {
            if (visible)
            {
                if (!IsWindowRectValid())
                    ResetWindowRect();

                _canShow = true;
            }
        }

        public void ResetWindowRect()
        {
            var screenRect = new Rect(
                x: ScreenOffset,
                y: ScreenOffset,
                width: Screen.width - ScreenOffset * 2,
                height: Screen.height - ScreenOffset * 2 - WindowManager.Instance.Height);
            WindowRect = GetDefaultWindowRect(screenRect);
        }

        private bool IsWindowRectValid()
        {
            return WindowRect.width >= MinimumSize.x &&
                   WindowRect.height >= MinimumSize.y &&
                   WindowRect.x < Screen.width - ScreenOffset &&
                   WindowRect.y < Screen.height - ScreenOffset &&
                   WindowRect.x >= -WindowRect.width + ScreenOffset &&
                   WindowRect.y >= -WindowRect.height + ScreenOffset;
        }

        protected abstract Rect GetDefaultWindowRect(Rect screenRect);

        public static Rect MakeDefaultWindowRect(Rect screenRect, TextAlignment side)
        {
            switch (side)
            {
                case TextAlignment.Left:
                    return new Rect(screenRect.xMin, screenRect.yMin, SideWidth, screenRect.height / 2);

                case TextAlignment.Center:
                    var centerWidth = (int)Mathf.Min(850, screenRect.width);
                    var centerX = (int)(screenRect.xMin + screenRect.width / 2 - Mathf.RoundToInt((float)centerWidth / 2));

                    var inspectorHeight = (int)(screenRect.height / 4) * 3;
                    return new Rect(centerX, screenRect.yMin, centerWidth, inspectorHeight);

                case TextAlignment.Right:
                    return new Rect(screenRect.xMax - SideWidth, screenRect.yMin, SideWidth, screenRect.height);

                default:
                    throw new ArgumentOutOfRangeException(nameof(side), side, null);
            }
        }
        
        protected abstract void DrawContents();

        public virtual string Title { get; set; }
        public int WindowId { get; set; }

        public virtual Rect WindowRect
        {
            get => _windowRect;
            set
            {
                if (_windowRect != value)
                {
                    _windowRect = value;
                    _confRect?.Invoke(value);
                }
            }
        }

        public Vector2 MinimumSize { get; set; } = new Vector2(100, 100);
    }
}
