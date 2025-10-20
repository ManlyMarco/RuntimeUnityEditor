using System;
using System.Collections.Generic;
using System.Linq;
using RuntimeUnityEditor.Core.REPL;
using UnityEngine;

namespace RuntimeUnityEditor.Core
{
    /// <summary>
    /// API for organizing and managing windows.
    /// </summary>
    public class WindowManager
    {
        /// <summary>
        /// Default distance of windows from screen corners and other windows.
        /// </summary>
        private const int ScreenMargin = 10;

        /// <summary>
        /// Default width of windows shown on left and right sides.
        /// </summary>
        private const int SideWidth = 350;

        /// <summary>
        /// Get default size of a window for a given screen client rectangle and desired screen partition.
        /// </summary>
        public static Rect MakeDefaultWindowRect(Rect screenClientRect, ScreenPartition screenPartition)
        {
            switch (screenPartition)
            {
                case ScreenPartition.Left:
                    return EnsureVisible(new Rect(screenClientRect.xMin, screenClientRect.yMin, SideWidth, screenClientRect.height));
                case ScreenPartition.LeftUpper:
                    return EnsureVisible(new Rect(screenClientRect.xMin, screenClientRect.yMin, SideWidth, screenClientRect.height / 2 - ScreenMargin));
                case ScreenPartition.LeftLower:
                    return EnsureVisible(new Rect(screenClientRect.xMin, screenClientRect.yMin + screenClientRect.height / 2, SideWidth, screenClientRect.height / 2));

                case ScreenPartition.Center:
                    {
                        var centerWidth = (int)Mathf.Min(850, screenClientRect.width);
                        var centerX = (int)(screenClientRect.xMin + screenClientRect.width / 2 - Mathf.RoundToInt((float)centerWidth / 2));
                        return EnsureVisible(new Rect(centerX, screenClientRect.yMin, centerWidth, screenClientRect.height));
                    }
                case ScreenPartition.CenterUpper:
                    {
                        var centerWidth = (int)Mathf.Min(850, screenClientRect.width);
                        var centerX = (int)(screenClientRect.xMin + screenClientRect.width / 2 - Mathf.RoundToInt((float)centerWidth / 2));
                        var upperHeight = (int)(screenClientRect.height / 4) * 3;
                        return EnsureVisible(new Rect(centerX, screenClientRect.yMin, centerWidth, upperHeight));
                    }
                case ScreenPartition.CenterLower:
                    {
                        var centerWidth = (int)Mathf.Min(850, screenClientRect.width);
                        var centerX = (int)(screenClientRect.xMin + screenClientRect.width / 2 - Mathf.RoundToInt((float)centerWidth / 2));
                        var upperHeight = (int)(screenClientRect.height / 4) * 3;
                        return EnsureVisible(new Rect(centerX, screenClientRect.yMin + upperHeight + ScreenMargin, centerWidth, screenClientRect.height - upperHeight - ScreenMargin));
                    }

                case ScreenPartition.Right:
                    return EnsureVisible(new Rect(screenClientRect.xMax - SideWidth, screenClientRect.yMin, SideWidth, screenClientRect.height));
                case ScreenPartition.RightUpper:
                    return EnsureVisible(new Rect(screenClientRect.xMax - SideWidth, screenClientRect.yMin, SideWidth, screenClientRect.height / 2));
                case ScreenPartition.RightLower:
                    return EnsureVisible(new Rect(screenClientRect.xMax - SideWidth, screenClientRect.yMin + screenClientRect.height / 2, SideWidth, screenClientRect.height / 2));

                case ScreenPartition.Full:
                    return screenClientRect;

                case ScreenPartition.Default:
                    if (ReplWindow.Initialized)
                        goto case ScreenPartition.CenterUpper;
                    else
                        goto case ScreenPartition.Center;

                default:
                    throw new ArgumentOutOfRangeException(nameof(screenPartition), screenPartition, null);
            }
        }

        internal static readonly List<IWindow> AdditionalWindows = new List<IWindow>();

        private static Rect EnsureVisible(Rect rect)
        {
            var result = rect;
            var allWindows = RuntimeUnityEditorCore.Instance.InitializedFeatures.OfType<IWindow>().Concat(AdditionalWindows);
            var allWindowsRects = allWindows.Select(w => w.WindowRect).ToList();
            // Check if any window near this position, move the rect until it's not near any other window
            while (allWindowsRects.Any(r => Mathf.Abs(r.x - result.x) < 7 && Mathf.Abs(r.y - result.y) < 7))
            {
                result.x += 17;
                result.y += 17;
            }
            // Ensure the new rect is visible on screen
            return result.x < Screen.width - 50 && result.y < Screen.height - 50 ? result : rect;
        }

        /// <summary>
        /// Discard current window size and position, and set the default ones for the given window.
        /// </summary>
        public static void ResetWindowRect(IWindow window)
        {
            var screenClientRect = new Rect(
                x: ScreenMargin,
                y: ScreenMargin,
                width: Screen.width - ScreenMargin * 2,
                height: Screen.height - ScreenMargin * 2 - Taskbar.Instance.Height);

            window.WindowRect = MakeDefaultWindowRect(screenClientRect, window.DefaultScreenPosition);
        }

        /// <summary>
        /// Check if the window rect of a given window is visible on the screen and of appropriate size.
        /// </summary>
        public static bool IsWindowRectValid(IWindow window)
        {
            return window.WindowRect.width >= window.MinimumSize.x &&
                   window.WindowRect.height >= window.MinimumSize.y &&
                   window.WindowRect.x < Screen.width - ScreenMargin &&
                   window.WindowRect.y < Screen.height - ScreenMargin &&
                   window.WindowRect.x >= -window.WindowRect.width + ScreenMargin &&
                   window.WindowRect.y >= -window.WindowRect.height + ScreenMargin;
        }
    }
}
