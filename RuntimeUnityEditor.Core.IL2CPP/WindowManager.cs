using System;
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
            //todo if a window is alraedy visible in a given partition's position, return a slightly moved rect so both windows are easily visible (add 15,15 to position I guess, repeat until nothing is there or we run out of space)

            switch (screenPartition)
            {
                case ScreenPartition.Left:
                    return new Rect(screenClientRect.xMin, screenClientRect.yMin, SideWidth, screenClientRect.height);
                case ScreenPartition.LeftUpper:
                    return new Rect(screenClientRect.xMin, screenClientRect.yMin, SideWidth, screenClientRect.height / 2 - ScreenMargin);
                case ScreenPartition.LeftLower:
                    return new Rect(screenClientRect.xMin, screenClientRect.yMin + screenClientRect.height / 2, SideWidth, screenClientRect.height / 2);

                case ScreenPartition.Center:
                    {
                        var centerWidth = (int)Mathf.Min(850, screenClientRect.width);
                        var centerX = (int)(screenClientRect.xMin + screenClientRect.width / 2 - Mathf.RoundToInt((float)centerWidth / 2));
                        return new Rect(centerX, screenClientRect.yMin, centerWidth, screenClientRect.height);
                    }
                case ScreenPartition.CenterUpper:
                    {
                        var centerWidth = (int)Mathf.Min(850, screenClientRect.width);
                        var centerX = (int)(screenClientRect.xMin + screenClientRect.width / 2 - Mathf.RoundToInt((float)centerWidth / 2));
                        var upperHeight = (int)(screenClientRect.height / 4) * 3;
                        return new Rect(centerX, screenClientRect.yMin, centerWidth, upperHeight);
                    }
                case ScreenPartition.CenterLower:
                    {
                        var centerWidth = (int)Mathf.Min(850, screenClientRect.width);
                        var centerX = (int)(screenClientRect.xMin + screenClientRect.width / 2 - Mathf.RoundToInt((float)centerWidth / 2));
                        var upperHeight = (int)(screenClientRect.height / 4) * 3;
                        return new Rect(centerX, screenClientRect.yMin + upperHeight + ScreenMargin, centerWidth, screenClientRect.height - upperHeight - ScreenMargin);
                    }

                case ScreenPartition.Right:
                    return new Rect(screenClientRect.xMax - SideWidth, screenClientRect.yMin, SideWidth, screenClientRect.height);
                case ScreenPartition.RightUpper:
                    return new Rect(screenClientRect.xMax - SideWidth, screenClientRect.yMin, SideWidth, screenClientRect.height / 2);
                case ScreenPartition.RightLower:
                    return new Rect(screenClientRect.xMax - SideWidth, screenClientRect.yMin + screenClientRect.height / 2, SideWidth, screenClientRect.height / 2);

                case ScreenPartition.Full:
                    return screenClientRect;

                case ScreenPartition.Default:
                    if (ReplWindow.Initialized) //todo figure out if any windows want to be in the lower part? set default partition for inspector and profiler then
                        goto case ScreenPartition.CenterUpper;
                    else
                        goto case ScreenPartition.Center;

                default:
                    throw new ArgumentOutOfRangeException(nameof(screenPartition), screenPartition, null);
            }
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
