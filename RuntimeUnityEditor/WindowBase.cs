using System;
using System.Collections;
using RuntimeUnityEditor.Core.Utils;
using RuntimeUnityEditor.Core.Utils.Abstractions;
using UnityEngine;

namespace RuntimeUnityEditor.Core
{
    /// <summary>
    /// Feature for use with RuntimeUnityEditor that has a GUILayout window. Custom windows can be added with <see cref="RuntimeUnityEditorCore.AddFeature"/>.
    /// Consider using <see cref="Window{T}"/> instead of the bare interface.
    /// </summary>
    public interface IWindow : IFeature
    {
        /// <summary>
        /// Title of the window shown in the title bar and messages related to the window.
        /// </summary>
        string Title { get; set; }
        /// <summary>
        /// ID of the GUILayout window. Set to a unique value automatically during initialization.
        /// </summary>
        int WindowId { get; set; }
        /// <summary>
        /// Size and position of the window, including borders and title bar.
        /// </summary>
        Rect WindowRect { get; set; }
        /// <summary>
        /// Minimum size of the window (width, height).
        /// </summary>
        Vector2 MinimumSize { get; set; }
        /// <summary>
        /// Discard current window size and position, and set the default ones for this window.
        /// </summary>
        void ResetWindowRect();
    }

    /// <summary>
    /// Base implementation of <see cref="T:RuntimeUnityEditor.Core.IWindow" />.
    /// <typeparamref name="T" /> should be your derived class's Type, e.g. <code>public class MyWindow : Window&lt;MyWindow&gt;</code>
    /// </summary>
    /// <inheritdoc cref="IWindow" />
    public abstract class Window<T> : FeatureBase<T>, IWindow where T : Window<T>
    {
        /// <summary>
        /// Default distance of windows from screen corners and other windows.
        /// </summary>
        protected const int ScreenOffset = 10;
        /// <summary>
        /// Default width of windows shown on left and right sides.
        /// </summary>
        protected const int SideWidth = 350;

        /// <summary>
        /// Width of tooltips shown inside windows.
        /// </summary>
        private const int TooltipWidth = 400;

        // ReSharper disable StaticMemberInGenericType
        private static GUIStyle _tooltipStyle;
        private static GUIContent _tooltipContent;
        private static Texture2D _tooltipBackground;
        // ReSharper restore StaticMemberInGenericType

        private bool _canShow;
        private Rect _windowRect;
        private Action<Rect> _confRect;

        /// <summary>
        /// Create a new window instance, should only ever be called once.
        /// </summary>
        protected Window()
        {
            DisplayType = FeatureDisplayType.Window;
            SettingCategory = "Windows";
        }

        /// <inheritdoc cref="FeatureBase{T}.AfterInitialized"/>
        protected override void AfterInitialized(InitSettings initSettings)
        {
            base.AfterInitialized(initSettings);
            WindowId = base.GetHashCode();
            _confRect = initSettings.RegisterSetting(SettingCategory, DisplayName + " window size", WindowRect, string.Empty, b => WindowRect = b);
        }

        /// <inheritdoc cref="FeatureBase{T}.DisplayName"/>
        public override string DisplayName
        {
            get => _displayName ?? (_displayName = Title ?? base.DisplayName);
            set => _displayName = value;
        }

        /// <inheritdoc cref="FeatureBase{T}.OnGUI"/>
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

        /// <inheritdoc cref="FeatureBase{T}.OnVisibleChanged"/>
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

        /// <inheritdoc cref="FeatureBase{T}.VisibleChanged"/>
        protected override void VisibleChanged(bool visible)
        {
            if (visible)
            {
                if (!IsWindowRectValid())
                    ResetWindowRect();

                _canShow = true;
            }
        }

        /// <inheritdoc cref="IWindow.ResetWindowRect"/>
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

        /// <summary>
        /// Get default size of this window (including border and title bar) for the given screen size.
        /// </summary>
        protected abstract Rect GetDefaultWindowRect(Rect screenRect);

        /// <summary>
        /// Get default size of a window for a given resolution and side of the screen. Center is wider than the sides.
        /// </summary>
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

        /// <summary>
        /// Draw contents of the window.
        /// This runs inside of <see cref="GUILayout.Window(int,UnityEngine.Rect,UnityEngine.GUI.WindowFunction,string,UnityEngine.GUILayoutOption[])"/>
        /// so all <see cref="GUILayout"/> methods can be used to construct the interface.
        /// </summary>
        protected abstract void DrawContents();

        /// <inheritdoc cref="IWindow.Title"/>
        public virtual string Title { get; set; }
        /// <inheritdoc cref="IWindow.WindowId"/>
        public int WindowId { get; set; }

        /// <inheritdoc cref="IWindow.WindowRect"/>
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

        /// <inheritdoc cref="IWindow.MinimumSize"/>
        public Vector2 MinimumSize { get; set; } = new Vector2(100, 100);
    }
}
