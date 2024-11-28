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
        /// Default position of this window.
        /// </summary>
        ScreenPartition DefaultScreenPosition { get; }
    }

    /// <summary>
    /// Base implementation of <see cref="T:RuntimeUnityEditor.Core.IWindow" />.
    /// <typeparamref name="T" /> should be your derived class's Type, e.g. <code>public class MyWindow : Window&lt;MyWindow&gt;</code>
    /// </summary>
    /// <inheritdoc cref="IWindow" />
    public abstract class Window<T> : FeatureBase<T>, IWindow where T : Window<T>
    {
        /// <summary>
        /// Default width of tooltips shown inside windows.
        /// </summary>
        protected const int DefaultTooltipWidth = 400;

        /// <summary>
        /// Width of tooltips shown inside this window.
        /// </summary>
        public int TooltipWidth { get; set; } = DefaultTooltipWidth;

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

#pragma warning disable CS0618
            // Backwards compat with CheatTools
            if (DefaultScreenPosition == ScreenPartition.Default && GetDefaultWindowRect(new Rect(0, 0, 1600, 900)) != default)
                DefaultScreenPosition = ScreenPartition.LeftLower;
#pragma warning restore CS0618
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
            WindowRect = GUILayout.Window(WindowId, WindowRect, (GUI.WindowFunction)DrawContentsInt, title);
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

        private void DrawTooltip(Rect area)
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
                var tooltipWidth = Mathf.Min(TooltipWidth, WindowRect.width);
                var height = _tooltipStyle.CalcHeight(_tooltipContent, tooltipWidth) + 10;

                var currentEvent = Event.current;

                var x = currentEvent.mousePosition.x + tooltipWidth > area.width
                    ? area.width - tooltipWidth
                    : currentEvent.mousePosition.x;

                var y = currentEvent.mousePosition.y + 25 + height > area.height
                    ? currentEvent.mousePosition.y - height
                    : currentEvent.mousePosition.y + 25;

                GUI.Box(new Rect(x, y, tooltipWidth, height), GUI.tooltip, _tooltipStyle);
            }
        }

        /// <inheritdoc cref="FeatureBase{T}.OnVisibleChanged"/>
        protected override void OnVisibleChanged(bool visible)
        {
            // If the taskbar didn't have a chance to initialize yet, wait for a frame. Necessary to calculate free screen space.
            if (visible && Taskbar.Instance.Height == 0)
            {
                // todo more efficient way?
                IEnumerator DelayedVisible()
                {
                    yield return null;
                    base.OnVisibleChanged(Visible);
                }
                
                RuntimeUnityEditorCore.PluginObject.AbstractStartCoroutine(DelayedVisible());
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
                if (!WindowManager.IsWindowRectValid(this))
                    ResetWindowRect();

                _canShow = true;
            }
        }

        /// <summary>
        /// Discard current window size and position, and set the default ones for this window.
        /// </summary>
        public void ResetWindowRect()
        {
            WindowManager.ResetWindowRect(this);
        }

        /// <summary>
        /// Get default size of this window (including border and title bar) for the given screen rect.
        /// The screen rect includes only useful work area (i.e. it excludes any margins and taskbar).
        /// </summary>
        [Obsolete("No longer used, set DefaultScreenPosition instead", false)]
        protected virtual Rect GetDefaultWindowRect(Rect screenClientRect)
        {
            return default;
        }

        /// <summary>
        /// Get default size of a window for a given resolution and side of the screen. Center is wider than the sides.
        /// </summary>
        [Obsolete("Use set DefaultScreenPosition or use WindowManager.MakeDefaultWindowRect instead", true)]
        public static Rect MakeDefaultWindowRect(Rect screenClientRect, TextAlignment side)
        {
            switch (side)
            {
                case TextAlignment.Left:
                    return WindowManager.MakeDefaultWindowRect(screenClientRect, ScreenPartition.LeftUpper);

                case TextAlignment.Center:
                    return WindowManager.MakeDefaultWindowRect(screenClientRect, ScreenPartition.CenterUpper);

                case TextAlignment.Right:
                    return WindowManager.MakeDefaultWindowRect(screenClientRect, ScreenPartition.Right);

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

        /// <inheritdoc cref="IWindow.DefaultScreenPosition" />
        public ScreenPartition DefaultScreenPosition { get; protected set; } = ScreenPartition.Default;
    }
}
