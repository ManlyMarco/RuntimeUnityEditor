using System.Collections;
using System.Collections.Generic;
using System.Linq;
using RuntimeUnityEditor.Core.Utils;
using UnityEngine;

namespace RuntimeUnityEditor.Core.Gizmos
{
    public sealed class GizmoDrawer
    {
        public const string GizmoObjectName = "RuntimeEditor_Gizmo";

        private readonly List<IGizmoEntry> _lines = new List<IGizmoEntry>();
        private bool _show;

        public GizmoDrawer(MonoBehaviour coroutineTarget)
        {
            coroutineTarget.StartCoroutine(EndOfFrame());
        }

        public bool Show
        {
            get => ShowGizmos && (_show || ShowGizmosOutsideEditor);
            set => _show = value;
        }

        public static bool ShowGizmos { get; set; }
        public static bool ShowGizmosOutsideEditor { get; set; }

        public static void DisplayControls()
        {
            if (!UnityFeatureHelper.SupportsVectrosity) return;

            GUILayout.BeginHorizontal(GUI.skin.box);
            {
                ShowGizmos = GUILayout.Toggle(ShowGizmos, "Show gizmos for selection");
                ShowGizmosOutsideEditor = GUILayout.Toggle(ShowGizmosOutsideEditor, "Always show");
                GUILayout.FlexibleSpace();
            }
            GUILayout.EndHorizontal();
        }

        public void UpdateState(Transform rootTransform)
        {
            ClearGizmos();

            if (rootTransform != null && Show)
                UpdateStateInt(rootTransform);
        }

        private void ClearGizmos()
        {
            if (_lines.Count == 0) return;
            _lines.ForEach(x => x.Destroy());
            _lines.Clear();
        }

        private IEnumerator EndOfFrame()
        {
            while (true)
            {
                yield return new WaitForEndOfFrame();
                if (Show)
                {
                    foreach (var x in _lines)
                        x.Draw();
                }
                else
                {
                    ClearGizmos();
                }
            }
        }

        private void UpdateStateInt(Transform rootTransform)
        {
            var renderer = rootTransform.GetComponent<Renderer>();

            if (renderer == null)
            {
                foreach (Transform tr in rootTransform)
                    UpdateStateInt(tr);
                return;
            }

            var children = renderer.GetComponentsInChildren<Renderer>();

            // Force update the bounds
            if (renderer is SkinnedMeshRenderer s)
                s.updateWhenOffscreen = true;
            foreach (var child in children.OfType<SkinnedMeshRenderer>())
                child.updateWhenOffscreen = true;

            var bounds2D = new RendererGizmo(renderer, children);
            _lines.Add(bounds2D);
        }

        /*public class ColliderGizmo : IGizmoEntry
        {
            public ColliderGizmo(Collider collider)
            {
                if (collider is BoxCollider bc)
                {
                    //bc.size
                }
                else if (collider is SphereCollider sc)
                {
                    //sc.radius
                }
                //collider.
                VectorLine = new VectorLine(BoundsObjectName, new List<Vector2>(8), 1f, LineType.Discrete);
            }

            public void Destroy()
            {
                throw new System.NotImplementedException();
            }

            public void Draw()
            {
                throw new System.NotImplementedException();
            }
        }*/
    }
}
