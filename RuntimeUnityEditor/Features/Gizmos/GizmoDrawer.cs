using System;
using System.Collections.Generic;
using HarmonyLib;
using RuntimeUnityEditor.Core.Gizmos.lib;
using RuntimeUnityEditor.Core.ObjectTree;
using RuntimeUnityEditor.Core.Utils.Abstractions;
using UnityEngine;

namespace RuntimeUnityEditor.Core.Gizmos
{
    public sealed class GizmoDrawer : FeatureBase<GizmoDrawer>
    {
        private readonly List<KeyValuePair<Action<Component>, Component>> _drawList = new List<KeyValuePair<Action<Component>, Component>>();
        private GizmosInstance _gizmosInstance;

        protected override void Initialize(InitSettings initSettings)
        {
            Enabled = false;
            DisplayName = "Gizmos";
            ObjectTreeViewer.Instance.TreeSelectionChanged += UpdateState;
        }

        protected override void VisibleChanged(bool visible)
        {
            base.VisibleChanged(visible);

            if (visible)
            {
                if (_gizmosInstance == null)
                    _gizmosInstance = RuntimeUnityEditorCore.PluginObject.gameObject.AddComponent<GizmosInstance>();
                _gizmosInstance.enabled = true;
            }
            else if (_gizmosInstance != null)
                _gizmosInstance.enabled = false;

            lib.Gizmos.Enabled = visible;
        }

        //todo
        //public static void DisplayControls()
        //{
        //    if (Initialized)
        //    {
        //        GUILayout.BeginHorizontal(GUI.skin.box);
        //        {
        //            GUILayout.Label("Gizmos");
        //            GUILayout.FlexibleSpace();
        //            Instance.Enabled = GUILayout.Toggle(Instance.Enabled, "Show selection");
        //            //ShowGizmosOutsideEditor = GUILayout.Toggle(ShowGizmosOutsideEditor, "When closed");
        //        }
        //        GUILayout.EndHorizontal();
        //    }
        //}

        public void UpdateState(Transform rootTransform)
        {
            _drawList.Clear();

            var dbcType = AccessTools.TypeByName("DynamicBoneCollider");

            var allComponents = rootTransform.GetComponentsInChildren<Component>();
            foreach (var component in allComponents)
            {
                if (component is Renderer)
                    _drawList.Add(new KeyValuePair<Action<Component>, Component>(DrawRendererGizmo, component));
                else if (component is Transform)
                    _drawList.Add(new KeyValuePair<Action<Component>, Component>(DrawTransformGizmo, component));
                else if (component is Collider)
                    _drawList.Add(new KeyValuePair<Action<Component>, Component>(DrawColliderGizmo, component));
                else if (component.GetType() == dbcType)
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
                var offset = Vector3.zero;
                offset[cc.direction] = cc.height * 0.5f - cc.radius;
                DrawWireCapsule(cc.center + offset, cc.center - offset, cc.radius, Color.cyan);
            }
            else if (obj is BoxCollider bc)
            {
                lib.Gizmos.Cube(bc.transform.position + bc.center, bc.transform.rotation, bc.size, Color.cyan);
            }
            else if (obj is SphereCollider sc)
            {
                lib.Gizmos.Sphere(sc.transform.position + sc.center, sc.radius, Color.cyan);
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
            if (obj == null) return;

            var tv = Traverse.Create(obj);

            var mBound = (int)tv.Field("m_Bound").GetValue(); // Bound enum
            var color = mBound == 0 ? Color.yellow : Color.red; // 0 = Bound.Outside

            var mRadius = tv.Field("m_Radius").GetValue<float>();
            var mHeight = tv.Field("m_Height").GetValue<float>();
            var mCenter = tv.Field("m_Center").GetValue<Vector3>();
            var radius = mRadius * Mathf.Abs(obj.transform.lossyScale.z);
            var height = (mHeight - mRadius) * 0.5f;
            if (height <= 0f)
            {
                lib.Gizmos.Sphere(obj.transform.TransformPoint(mCenter), radius, color);
                return;
            }
            var center = mCenter;
            var center2 = mCenter;
            var mDirection = (int)tv.Field("m_Direction").GetValue(); // Direction enum
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
            //Popcron.Gizmos.Sphere(obj.transform.TransformPoint(center), radius, color);
            //Popcron.Gizmos.Sphere(obj.transform.TransformPoint(center2), radius, color);
            DrawWireCapsule(obj.transform.TransformPoint(center), obj.transform.TransformPoint(center2), radius, color);
        }

        // Based on code by Qriva
        private static void DrawWireCapsule(Vector3 p1, Vector3 p2, float radius, Color color)
        {
            // Special case when both points are in the same position
            if (p1 == p2)
            {
                lib.Gizmos.Sphere(p1, radius, Color.blue);
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
