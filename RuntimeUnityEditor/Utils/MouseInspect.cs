using UnityEngine;

namespace RuntimeUnityEditor.Core.Utils
{
    // Based on https://github.com/sinai-dev/CppExplorer/blob/master/src/Menu/InspectUnderMouse.cs (MIT License)
    public class MouseInspect
    {
        public static bool Enable { get; set; }

        private static Transform _objUnderMouse;
        private static string _hoverText;
        private static bool _clickedOnce;

        public static void Update()
        {
            if (Enable)
            {
                InspectRaycast();
            }
        }

        private static void InspectRaycast()
        {
            var camera = Camera.main;
            if (camera == null) return;

            var ray = camera.ScreenPointToRay(Input.mousePosition);

            if (Physics.Raycast(ray, out RaycastHit hit, 1000f))
            {
                var obj = hit.transform;

                if (_objUnderMouse != obj)
                {
                    _objUnderMouse = obj;
                    var position = obj.position;
                    _hoverText = $"{_objUnderMouse.GetFullTransfromPath()}\nPosition: X={position.x:F} Y={position.y:F} Z={position.z:F}";
                    if (!_clickedOnce) _hoverText += "\nClick to select";
                }

                if (Input.GetMouseButtonDown(0))
                {
                    _clickedOnce = true;
                    RuntimeUnityEditorCore.Instance.TreeViewer.SelectAndShowObject(obj);
                }
            }
            else
            {
                _objUnderMouse = null;
            }
        }

        public static void OnGUI()
        {
            if (Enable && _objUnderMouse != null)
            {
                var pos = Input.mousePosition;
                var rect = new Rect(pos.x - (int)(Screen.width / 2), Screen.height - pos.y - 50, Screen.width, 50);

                var origAlign = GUI.skin.label.alignment;
                GUI.skin.label.alignment = TextAnchor.MiddleCenter;

                IMGUIUtils.DrawLabelWithOutline(rect, _hoverText, GUI.skin.label, Color.white, Color.black, 1);

                GUI.skin.label.alignment = origAlign;
            }
        }
    }
}
