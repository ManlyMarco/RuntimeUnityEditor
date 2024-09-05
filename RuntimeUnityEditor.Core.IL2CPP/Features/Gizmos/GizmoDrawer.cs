using System;
using System.Collections.Generic;
using BepInEx.Unity.IL2CPP;
using HarmonyLib;
using RuntimeUnityEditor.Core.Gizmos.lib;
using RuntimeUnityEditor.Core.ObjectTree;
using RuntimeUnityEditor.Core.Utils.Abstractions;
using UnityEngine;
#pragma warning disable CS1591

namespace RuntimeUnityEditor.Core.Gizmos
{
    //todo ShowGizmosOutsideEditor
    /// <summary>
    /// Feature that shows gizmos for selected GameObjects.
    /// </summary>
    public sealed class GizmoDrawer : FeatureBase<GizmoDrawer>
    {
        private readonly List<KeyValuePair<Action<Component>, Component>> _drawList = new List<KeyValuePair<Action<Component>, Component>>();
        private GizmosInstance _gizmosInstance;
        private Type _dbcType;
        private bool _dbcTypeAttempted;

        protected override void Initialize(InitSettings initSettings)
        {
            UnityFeatureHelper.EnsureCameraRenderEventsAreAvailable();
            
            Enabled = false;
            DisplayName = "Gizmos";
            ObjectTreeViewer.Instance.TreeSelectionChanged += UpdateState;
        }

        protected override void VisibleChanged(bool visible)
        {
            base.VisibleChanged(visible);

            _drawList.Clear();

            if (visible)
            {
                if (!_gizmosInstance)
                    _gizmosInstance = IL2CPPChainloader.AddUnityComponent<GizmosInstance>(); //RuntimeUnityEditorCore.PluginObject.gameObject.AddComponent<GizmosInstance>();
                _gizmosInstance.enabled = true;

                UpdateState(ObjectTreeViewer.Instance.SelectedTransform);
            }
            else if (_gizmosInstance)
            {
                _gizmosInstance.enabled = false;
            }

            lib.Gizmos.Enabled = visible;
        }

        public void UpdateState(Transform rootTransform)
        {
            if (!Visible || rootTransform == null) return;

            _drawList.Clear();

            if (!_dbcTypeAttempted)
            {
                _dbcTypeAttempted = true;
                _dbcType = AccessTools.TypeByName("DynamicBoneCollider");
            }

            var allComponents = rootTransform.GetAllComponentsInChildrenCasted(false); //todo include inactive with different color?
            foreach (var component in allComponents)
            {
                if (!component)
                    continue;
                else if (component is Renderer)
                    _drawList.Add(new KeyValuePair<Action<Component>, Component>(DrawRendererGizmo, component));
                else if (component is Transform)
                    _drawList.Add(new KeyValuePair<Action<Component>, Component>(DrawTransformGizmo, component));
                else if (component is Collider)
                    _drawList.Add(new KeyValuePair<Action<Component>, Component>(DrawColliderGizmo, component));
                else if (component is Projector)
                    _drawList.Add(new KeyValuePair<Action<Component>, Component>(DrawProjectorGizmo, component));
                else if (_dbcType != null && component.GetType() == _dbcType)
                    _drawList.Add(new KeyValuePair<Action<Component>, Component>(DrawDynamicBoneColliderGizmo, component));

            }

            //foreach (var r in _targets.OfType<SkinnedMeshRenderer>())
            //{
            //    // Force update the bounds, has side effects
            //    //r.updateWhenOffscreen = true;
            //    
            //}
        }

        private static void DrawColliderGizmo(Component obj)
        {
            if (obj == null) return;

            if (obj is CapsuleCollider cc)
            {
                var lossyScale = cc.transform.lossyScale;
                var radiusScale = Mathf.Max(Mathf.Abs(lossyScale.x) * (cc.direction != 0 ? 1 : 0),
                                        Mathf.Abs(lossyScale.y) * (cc.direction != 1 ? 1 : 0),
                                        Mathf.Abs(lossyScale.z) * (cc.direction != 2 ? 1 : 0));
                var heightScale = lossyScale[cc.direction];

                var radiusScaled = cc.radius * radiusScale;
                var offset = Vector3.zero;
                var height = Mathf.Max(Mathf.Abs(cc.height * heightScale), radiusScaled);
                offset[cc.direction] = (height - radiusScaled) / 2f;
                // take rotation into account
                offset = cc.transform.rotation * offset;
                var center = Vector3.Scale(cc.center, lossyScale);
                DrawWireCapsule(cc.transform.position + center + offset, cc.transform.position + center - offset, radiusScaled, Color.cyan);
            }
            else if (obj is BoxCollider bc)
            {
                lib.Gizmos.Cube(bc.transform.position + bc.center, bc.transform.rotation, Vector3.Scale(bc.transform.lossyScale, bc.size), Color.cyan);
            }
            else if (obj is SphereCollider sc)
            {
                //todo rotation? not really needed
                var lossyScale = sc.transform.lossyScale;
                lib.Gizmos.Sphere(sc.transform.position + sc.center, sc.radius * Mathf.Max(lossyScale.x, lossyScale.y, lossyScale.z), Color.cyan);
            }
            else if (obj is MeshCollider mc)
            {
                // cop out
                lib.Gizmos.Bounds(mc.bounds, Color.magenta);
            }
            else if (obj is TerrainCollider tc)
            {
                // cop out
                lib.Gizmos.Bounds(tc.bounds, Color.magenta);
            }
            else
            {
                RuntimeUnityEditorCore.Logger.Log(LogLevel.Warning, "Unhandled collider type: " + obj.GetType());
            }
        }

        private static void DrawTransformGizmo(Component obj)
        {
            if (obj != null && obj is Transform tr)
            {
                //todo configurable scale
                lib.Gizmos.Line(tr.position, tr.position + tr.forward * 0.05f, Color.green);
                lib.Gizmos.Line(tr.position, tr.position + tr.right * 0.05f, Color.red);
                lib.Gizmos.Line(tr.position, tr.position + tr.up * 0.05f, Color.blue);
            }
        }

        private static void DrawRendererGizmo(Component obj)
        {
            if (obj != null && obj is Renderer rend)
                lib.Gizmos.Bounds(rend.bounds, Color.green);
        }

        private static void DrawDynamicBoneColliderGizmo(Component obj)
        {
            if (!obj) return;

            var transform = obj.transform;
            var tv = Traverse.Create(obj);

            // Fields are properties in IL2CPP
            var mBound = (int)tv.Property("m_Bound").GetValue(); // Bound enum
            var color = mBound == 0 ? Color.yellow : Color.red; // 0 = Bound.Outside

            var mRadius = tv.Property("m_Radius").GetValue<float>();
            var mHeight = tv.Property("m_Height").GetValue<float>();
            var mCenter = tv.Property("m_Center").GetValue<Vector3>();
            var radius = mRadius * Mathf.Abs(transform.lossyScale.z);
            var height = (mHeight - mRadius) * 0.5f;
            if (height <= 0f)
            {
                lib.Gizmos.Sphere(transform.TransformPoint(mCenter), radius, color);
                return;
            }
            var center = mCenter;
            var center2 = mCenter;
            var mDirection = (int)tv.Property("m_Direction").GetValue(); // Direction enum
            switch (mDirection)
            {
                case 0: //Direction.X:
                    center.x -= height;
                    center2.x += height;
                    break;
                case 1: //Direction.Y:
                    center.y -= height;
                    center2.y += height;
                    break;
                case 2: //Direction.Z:
                    center.z -= height;
                    center2.z += height;
                    break;
            }
            DrawWireCapsule(transform.TransformPoint(center), transform.TransformPoint(center2), radius, color);
        }

        private static void DrawProjectorGizmo(Component obj)
        {
            if (obj && obj is Projector projector)
            {
                float startRange, endRange;
                if (projector.orthographic)
                {
                    startRange = projector.orthographicSize;
                    endRange = projector.orthographicSize;
                }
                else
                {
                    startRange = projector.nearClipPlane * Mathf.Tan(Mathf.PI / 180f * projector.fieldOfView / 2f);
                    endRange = projector.farClipPlane * Mathf.Tan(Mathf.PI / 180f * projector.fieldOfView / 2f);
                }

                Vector3 forward = projector.transform.rotation * Vector3.forward;
                Vector3 up = projector.transform.rotation * Vector3.up;
                Vector3 right = projector.transform.rotation * Vector3.right;

                Vector3 startTopRight, startBottomRight, startTopLeft, startBottomLeft, endTopRight, endBottomRight, endTopLeft, endBottomLeft;
                startTopRight = projector.transform.position + forward * projector.nearClipPlane + (up + right * projector.aspectRatio) * startRange;
                startBottomRight = projector.transform.position + forward * projector.nearClipPlane + (-up + right * projector.aspectRatio) * startRange;
                startTopLeft = projector.transform.position + forward * projector.nearClipPlane + (up - right * projector.aspectRatio) * startRange;
                startBottomLeft = projector.transform.position + forward * projector.nearClipPlane + (-up - right * projector.aspectRatio) * startRange;

                endTopRight = projector.transform.position + forward * projector.farClipPlane + (up + right * projector.aspectRatio) * endRange;
                endBottomRight = projector.transform.position + forward * projector.farClipPlane + (-up + right * projector.aspectRatio) * endRange;
                endTopLeft = projector.transform.position + forward * projector.farClipPlane + (up - right * projector.aspectRatio) * endRange;
                endBottomLeft = projector.transform.position + forward * projector.farClipPlane + (-up - right * projector.aspectRatio) * endRange;

                //Draw near clip plane
                lib.Gizmos.Line(startTopRight, startBottomRight, Color.red);
                lib.Gizmos.Line(startBottomRight, startBottomLeft, Color.red);
                lib.Gizmos.Line(startBottomLeft, startTopLeft, Color.red);
                lib.Gizmos.Line(startTopLeft, startTopRight, Color.red);

                //Draw far clip plane
                lib.Gizmos.Line(endTopRight, endBottomRight, Color.red);
                lib.Gizmos.Line(endBottomRight, endBottomLeft, Color.red);
                lib.Gizmos.Line(endBottomLeft, endTopLeft, Color.red);
                lib.Gizmos.Line(endTopLeft, endTopRight, Color.red);

                //Draw connection between near and far clip planes
                lib.Gizmos.Line(startTopRight, endTopRight, Color.red);
                lib.Gizmos.Line(startBottomRight, endBottomRight, Color.red);
                lib.Gizmos.Line(startTopLeft, endTopLeft, Color.red);
                lib.Gizmos.Line(startBottomLeft, endBottomLeft, Color.red);
            }
        }

        // Based on code by Qriva
        private static void DrawWireCapsule(Vector3 p1, Vector3 p2, float radius, Color color)
        {
            //Console.WriteLine($"{p1} {p2} {radius} {color}");
            // Special case when both points are in the same position
            if (p1 == p2)
            {
                lib.Gizmos.Sphere(p1, radius, color);
                return;
            }

            var p1Rotation = Quaternion.LookRotation(p1 - p2);
            var p2Rotation = Quaternion.LookRotation(p2 - p1);
            // Check if capsule direction is collinear to Vector.up
            var c = Vector3.Dot((p1 - p2).normalized, Vector3.up);
            if (c == 1f || c == -1f)
            {
                // Fix rotation
                p2Rotation = Quaternion.Euler(p2Rotation.eulerAngles.x, p2Rotation.eulerAngles.y + 180f, p2Rotation.eulerAngles.z);
            }
            // First side
            lib.Gizmos.Arc(p1, radius, p1Rotation * Quaternion.Euler(90, 0, 0), 0f, 180f, color);
            lib.Gizmos.Arc(p1, radius, p1Rotation * Quaternion.Euler(0, 90, 0), 90f, 180f, color);
            lib.Gizmos.Circle(p1, radius, p1Rotation, color);
            // Second side
            lib.Gizmos.Arc(p2, radius, p2Rotation * Quaternion.Euler(90, 0, 0), 0f, 180f, color);
            lib.Gizmos.Arc(p2, radius, p2Rotation * Quaternion.Euler(0, 90, 0), 90f, 180f, color);
            lib.Gizmos.Circle(p2, radius, p2Rotation, color);
            // Lines
            lib.Gizmos.Line(p1 + p1Rotation * Vector3.down * radius, p2 + p2Rotation * Vector3.down * radius, color);
            lib.Gizmos.Line(p1 + p1Rotation * Vector3.left * radius, p2 + p2Rotation * Vector3.right * radius, color);
            lib.Gizmos.Line(p1 + p1Rotation * Vector3.up * radius, p2 + p2Rotation * Vector3.up * radius, color);
            lib.Gizmos.Line(p1 + p1Rotation * Vector3.right * radius, p2 + p2Rotation * Vector3.left * radius, color);
        }

        protected override void LateUpdate()
        {
            for (var i = 0; i < _drawList.Count; i++)
            {
                var kvp = _drawList[i];
                kvp.Key(kvp.Value);
            }
        }
    }
}
