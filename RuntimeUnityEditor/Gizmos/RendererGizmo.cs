using System.Collections.Generic;
using RuntimeUnityEditor.Core.Utils;
using UnityEngine;
using Vectrosity;

namespace RuntimeUnityEditor.Core.Gizmos
{
    public class RendererGizmo : IGizmoEntry
    {
        private readonly Renderer _renderer;
        private readonly Renderer[] _childRenderers;
        public VectorLine VectorLine;

        public RendererGizmo(Renderer renderer, Renderer[] childRenderers)
        {
            _renderer = renderer;
            _childRenderers = childRenderers;
            VectorLine = new VectorLine(GizmoDrawer.GizmoObjectName + "_Renderer2D", new List<Vector2>(8), 1f, LineType.Discrete);
        }

        public void Destroy()
        {
            VectorLine.Destroy(ref VectorLine);
        }

        public void Draw()
        {
            if (VectorLine == null || !_renderer) return;

            var bounds = _renderer.bounds;
            if (_childRenderers.Length > 0)
                bounds.Encapsulate(BoundsUtils.CombineBounds(_childRenderers));

            var rect = bounds.BoundsToScreenRect(Camera.main);
            VectorLine.MakeRect(rect);
            VectorLine.SetColor(_renderer.gameObject.activeInHierarchy ? new Color(0.4f, 0.95f, 0.4f) : new Color(0.65f, 0.65f, 0.65f));
            VectorLine.Draw();
        }
    }
}
