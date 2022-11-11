using System.Collections.Generic;
using System.Linq;
using System.Text;
using RuntimeUnityEditor.Core.ObjectTree;
using RuntimeUnityEditor.Core.Utils;
using RuntimeUnityEditor.Core.Utils.Abstractions;
using UnityEngine;

namespace RuntimeUnityEditor.Core
{
    public class MouseInspect : FeatureBase<MouseInspect>
    {
        private static readonly StringBuilder _hoverTextSb = new StringBuilder(100);
        private static string _hoverText = string.Empty;
        private GUIStyle _labelSkin;

        protected override void Initialize(InitSettings initSettings)
        {
            DisplayName = "Mouse inspect";
        }

        protected override void Update()
        {
            var camera = Camera.main;
            var raycastHit = DoRaycast(camera);
            var canvasHits = DoCanvas(camera);

            var selectedTransform = ObjectTreeViewer.Instance.SelectedTransform;

            _hoverTextSb.Length = 0;

            if (raycastHit != null)
            {
                _hoverTextSb.AppendFormat("Raycast hit:\n{0}0: {1} pos={2}\n", selectedTransform == raycastHit ? "> " : "", raycastHit.name, raycastHit.position);
            }

            if (canvasHits.Count > 0)
            {
                _hoverTextSb.AppendLine("Canvas hits:");
                for (var index = 0; index < canvasHits.Count && index < 10; index++)
                {
                    var hit = canvasHits[index];
                    _hoverTextSb.AppendFormat("{0}{1}: {2} pos={3} size={4}\n", selectedTransform == hit ? "> " : "", index + 1, hit.name, hit.position, hit.sizeDelta);
                }
            }

            if (_hoverTextSb.Length > 0)
            {
                if (ObjectTreeViewer.Initialized)
                {
                    _hoverTextSb.Append("[ Press Middle Mouse Button to browse to the next object ]");
                    if (UnityInput.Current.GetMouseButtonDown(2))
                    {
                        var all = Enumerable.Repeat(raycastHit, 1).Concat(canvasHits.Cast<Transform>()).Where(x => x != null).ToList();

                        var currentIndex = all.IndexOf(selectedTransform);
                        // If nothing is selected -1 + 1 = 0 so start from 0
                        var nextObject = all.Concat(all).Skip(currentIndex + 1).First();

                        ObjectTreeViewer.Instance.SelectAndShowObject(nextObject);
                    }
                }

                _hoverText = _hoverTextSb.ToString();
            }
            else
            {
                _hoverText = string.Empty;
            }
        }

        protected override void OnGUI()
        {
            if (_hoverText.Length > 0)
            {
                if (_labelSkin == null) _labelSkin = new GUIStyle(GUI.skin.label);

                // Figure out which corner of the screen to draw the hover text in
                _labelSkin.alignment = TextAnchor.UpperLeft;

                var pos = UnityInput.Current.mousePosition;
                var displayRect = new Rect((int)pos.x + 5, Screen.height - (int)pos.y + 20, (int)(Screen.width / 2), 500);

                if ((int)displayRect.x > Screen.width / 2)
                {
                    displayRect.x = (int)(displayRect.x - displayRect.width);
                    _labelSkin.alignment = TextAnchor.UpperRight;
                }

                if ((int)displayRect.y > Screen.height / 2)
                {
                    displayRect.y = (int)(displayRect.y - (displayRect.height + 23));
                    // Upper -> Lower
                    _labelSkin.alignment += TextAnchor.LowerRight - TextAnchor.UpperRight;
                }

                IMGUIUtils.DrawLabelWithOutline(displayRect, _hoverText, _labelSkin, Color.white, Color.black, 2);
            }
        }

        private static Transform DoRaycast(Camera camera)
        {
            if (camera == null) return null;
            // Based on https://github.com/sinai-dev/CppExplorer/blob/master/src/Menu/InspectUnderMouse.cs (MIT License)
            var ray = camera.ScreenPointToRay(UnityInput.Current.mousePosition);
            return Physics.Raycast(ray, out RaycastHit hit, 1000f) ? hit.transform : null;
        }

        private static List<RectTransform> DoCanvas(Camera camera)
        {
            Vector2 mousePosition = UnityInput.Current.mousePosition;

            var hits = Object.FindObjectsOfType<RectTransform>().Where(rt =>
            {
                // Only RTs that are visible on screen
                if (rt && rt.sizeDelta.x > 0 && rt.sizeDelta.y > 0 && rt.gameObject.activeInHierarchy)
                {
                    var canvas = rt.GetComponentInParent<Canvas>();
                    if (canvas != null && canvas.enabled && rt.GetComponentsInParent<CanvasGroup>().All(x => x.alpha > 0.1f))
                    {
                        // Figure out what camera this canvas is drawn to, overlay draws to a hidden camera that's accessed by a null
                        var cam = canvas.renderMode == RenderMode.ScreenSpaceOverlay ? null : canvas.worldCamera != null ? canvas.worldCamera : camera;
                        return RectTransformUtility.RectangleContainsScreenPoint(rt, mousePosition, cam);
                    }
                }
                return false;
            });

            // Smallest rect first since generally UI elements get progressively smaller inside each other, and we mostly care about buttons which are the smallest
            return hits.OrderBy(x => x.sizeDelta.sqrMagnitude).ToList();
        }
    }
}
