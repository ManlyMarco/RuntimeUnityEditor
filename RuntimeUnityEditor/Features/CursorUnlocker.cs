using System;
using System.Reflection;
using UnityEngine;

namespace RuntimeUnityEditor.Core
{
    public sealed class CursorUnlocker : FeatureBase<CursorUnlocker>
    {
        private bool _obsoleteCursor;

        private PropertyInfo _curLockState;
        private PropertyInfo _curVisible;

        private int _previousCursorLockState;
        private bool _previousCursorVisible;

        protected override void Initialize(RuntimeUnityEditorCore.InitSettings initSettings)
        {
            // Reflection for compatibility with Unity 4.x
            var tCursor = typeof(Cursor);

            _curLockState = tCursor.GetProperty("lockState", BindingFlags.Static | BindingFlags.Public);
            _curVisible = tCursor.GetProperty("visible", BindingFlags.Static | BindingFlags.Public);

            if (_curLockState == null || _curVisible == null)
            {
                _obsoleteCursor = true;

                _curLockState = typeof(Screen).GetProperty("lockCursor", BindingFlags.Static | BindingFlags.Public);
                _curVisible = typeof(Screen).GetProperty("showCursor", BindingFlags.Static | BindingFlags.Public);

                if (_curLockState == null || _curVisible == null)
                    throw new InvalidOperationException("Unsupported Cursor class");
            }
        }

        protected override void Update()
        {
            if (_obsoleteCursor)
                _curLockState.SetValue(null, false, null);
            else
                _curLockState.SetValue(null, 0, null);

            _curVisible.SetValue(null, true, null);
        }

        protected override void LateUpdate()
        {
            if (_obsoleteCursor)
                _curLockState.SetValue(null, false, null);
            else
                _curLockState.SetValue(null, 0, null);

            _curVisible.SetValue(null, true, null);
        }

        protected override void OnGUI()
        {
            if (_obsoleteCursor)
                _curLockState.SetValue(null, false, null);
            else
                _curLockState.SetValue(null, 0, null);

            _curVisible.SetValue(null, true, null);
        }

        protected override void VisibleChanged(bool visible)
        {
            if (visible)
            {
                _previousCursorLockState = _obsoleteCursor ? Convert.ToInt32((bool)_curLockState.GetValue(null, null)) : (int)_curLockState.GetValue(null, null);
                _previousCursorVisible = (bool)_curVisible.GetValue(null, null);

            }
            else
            {
                if (!_previousCursorVisible || _previousCursorLockState != 0)
                {
                    if (_obsoleteCursor)
                        _curLockState.SetValue(null, Convert.ToBoolean(_previousCursorLockState), null);
                    else
                        _curLockState.SetValue(null, _previousCursorLockState, null);

                    _curVisible.SetValue(null, _previousCursorVisible, null);
                }
            }
        }
    }
}
