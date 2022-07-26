using System;
using RuntimeUnityEditor.Core.Utils;
using UnityEngine;

namespace RuntimeUnityEditor.Core
{
    public abstract class WindowBase<T> where T : WindowBase<T>
    {
        public static T Instance { get; private set; }

        public WindowBase()
        {
            WindowId = base.GetHashCode();
            WindowBase<T>.Instance = (T)this;
        }

        internal void DrawWindow()
        {
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
            }
            catch (Exception ex)
            {
                RuntimeUnityEditorCore.Logger.Log(LogLevel.Error, $"[{Title}] GUI crash: {ex}");
            }

            WindowRect = IMGUIUtils.DragResizeEat(id, WindowRect);
        }

        protected abstract void DrawContents();

        public virtual bool Enabled { get; set; }
        public virtual string Title { get; set; }
        public int WindowId { get; set; }
        public virtual Rect WindowRect { get; set; }
        public Vector2 MinimumSize { get; set; } = new Vector2(100, 100);
    }
}
