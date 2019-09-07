using System;
using System.Collections.Generic;
using UnityEngine;

namespace RuntimeUnityEditor.Core.Utils
{
    public static class BoundsUtils
    {
        public static Bounds CombineBounds(IEnumerable<Renderer> renderers)
        {
            Bounds? b = null;
            foreach (var renderer in renderers)
            {
                if (b == null)
                    b = renderer.bounds;
                else
                    b.Value.Encapsulate(renderer.bounds);
            }

            if (b == null)
                throw new ArgumentOutOfRangeException(nameof(renderers), "Need at least one renderer");

            return b.Value;
        }

        public static Rect BoundsToScreenRect(this Bounds bounds, Camera camera)
        {
            var cen = bounds.center;
            var ext = bounds.extents;
            var extentPoints = new Vector2[8]
            {
                camera.WorldToScreenPoint(new Vector3(cen.x-ext.x, cen.y-ext.y, cen.z-ext.z)),
                camera.WorldToScreenPoint(new Vector3(cen.x+ext.x, cen.y-ext.y, cen.z-ext.z)),
                camera.WorldToScreenPoint(new Vector3(cen.x-ext.x, cen.y-ext.y, cen.z+ext.z)),
                camera.WorldToScreenPoint(new Vector3(cen.x+ext.x, cen.y-ext.y, cen.z+ext.z)),
                camera.WorldToScreenPoint(new Vector3(cen.x-ext.x, cen.y+ext.y, cen.z-ext.z)),
                camera.WorldToScreenPoint(new Vector3(cen.x+ext.x, cen.y+ext.y, cen.z-ext.z)),
                camera.WorldToScreenPoint(new Vector3(cen.x-ext.x, cen.y+ext.y, cen.z+ext.z)),
                camera.WorldToScreenPoint(new Vector3(cen.x+ext.x, cen.y+ext.y, cen.z+ext.z))
            };

            var min = extentPoints[0];
            var max = extentPoints[0];
            foreach (var v in extentPoints)
            {
                min = Vector2.Min(min, v);
                max = Vector2.Max(max, v);
            }

            return new Rect(min.x, min.y, max.x - min.x, max.y - min.y);
        }

        public static Vector2 WorldToGUIPoint(Vector3 world)
        {
            var screenPoint = Camera.main.WorldToScreenPoint(world);
            screenPoint.y = Screen.height - screenPoint.y;
            return screenPoint;
        }

        /*public static void VisualizeRenderers(List<Renderer> renderers, int type)
        {
            var skins = renderers.Select(x => x as SkinnedMeshRenderer).Where(x => x);
            foreach (var skin in skins) skin.updateWhenOffscreen = true;

            if ((type & 1) != 0)
            {
                var bounds3d = new GizmoDrawer.VectorLineUpdater();
                bounds3d.VectorLine = new VectorLine("Bounds3D", new List<Vector3>(24), 1f, LineType.Discrete);
                bounds3d.Draw = () =>
                {
                    var bounds = CombineBounds(renderers);
                    bounds3d.VectorLine.MakeCube(bounds.center, bounds.size.x, bounds.size.y, bounds.size.z);
                    bounds3d.VectorLine.SetColor(Color.red);
                    bounds3d.VectorLine.Draw();
                };
            }

            if ((type & 2) != 0)
            {
                var bounds2d = new GizmoDrawer.VectorLineUpdater();
                bounds2d.VectorLine = new VectorLine("Bounds2D", new List<Vector2>(8), 1f, LineType.Discrete);
                bounds2d.Draw = () =>
                {
                    var bounds = CombineBounds(renderers);
                    var rect = BoundsToScreenRect(bounds, Camera.main);
                    bounds2d.VectorLine.MakeRect(rect);
                    bounds2d.VectorLine.SetColor(Color.green);
                    bounds2d.VectorLine.Draw();
                };
            }
        }*/
    }
}