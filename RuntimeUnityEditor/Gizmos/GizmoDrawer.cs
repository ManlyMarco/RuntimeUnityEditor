using System;
using System.Collections.Generic;
using System.Linq;
using HarmonyLib;
using Popcron;
using RuntimeUnityEditor.Core.ObjectTree;
using UnityEngine;
using static RuntimeUnityEditor.Core.RuntimeUnityEditorCore;

namespace RuntimeUnityEditor.Core.Gizmos
{
    public sealed class GizmoDrawer : FeatureBase<GizmoDrawer>
    {
        private readonly List<KeyValuePair<Action<Component>, Component>> _drawList = new List<KeyValuePair<Action<Component>, Component>>();
        private GizmosInstance _gizmosInstance;

        protected override void Initialize(InitSettings initSettings)
        {
            Enabled = false;
            ObjectTreeViewer.Instance.TreeSelectionChanged += UpdateState;
        }

        public override bool Enabled
        {
            get => base.Enabled;
            set
            {
                if (base.Enabled != value)
                {
                    base.Enabled = value;

                    if (value)
                    {
                        if (_gizmosInstance == null)
                            _gizmosInstance = PluginObject.gameObject.AddComponent<GizmosInstance>();
                        _gizmosInstance.enabled = true;
                    }
                    else if (_gizmosInstance != null)
                        _gizmosInstance.enabled = false;

                    Popcron.Gizmos.Enabled = value;
                }
            }
        }

        public static void DisplayControls()
        {
            if (Initialized)
            {
                GUILayout.BeginHorizontal(GUI.skin.box);
                {
                    GUILayout.Label("Gizmos");
                    GUILayout.FlexibleSpace();
                    Instance.Enabled = GUILayout.Toggle(Instance.Enabled, "Show selection");
                    //ShowGizmosOutsideEditor = GUILayout.Toggle(ShowGizmosOutsideEditor, "When closed");
                }
                GUILayout.EndHorizontal();
            }
        }

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
                Popcron.Gizmos.Cube(bc.transform.position + bc.center, bc.transform.rotation, bc.size, Color.cyan);
            }
            else if (obj is SphereCollider sc)
            {
                Popcron.Gizmos.Sphere(sc.transform.position + sc.center, sc.radius, Color.cyan);
            }
            else if (obj is MeshCollider mc)
            {
                // cop out
                Popcron.Gizmos.Bounds(mc.bounds, Color.magenta);
            }
            else if (obj is TerrainCollider tc)
            {
                // cop out
                Popcron.Gizmos.Bounds(tc.bounds, Color.magenta);
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
                Popcron.Gizmos.Line(tr.position, tr.position + tr.forward * 0.05f, Color.green);
                Popcron.Gizmos.Line(tr.position, tr.position + tr.right * 0.05f, Color.red);
                Popcron.Gizmos.Line(tr.position, tr.position + tr.up * 0.05f, Color.blue);
            }
        }

        private static void DrawRendererGizmo(Component obj)
        {
            if (obj != null && obj is Renderer rend)
                Popcron.Gizmos.Bounds(rend.bounds, Color.green);
        }

        //todo
        private static void DrawDynamicBoneColliderGizmo(Component obj)
        {
            if (obj == null) return;

            var tv = Traverse.Create(obj);

            var m_Bound = (int)tv.Field("m_Bound").GetValue(); // Bound enum
            var color = m_Bound == 0 ? Color.yellow : Color.red; // 0 = Bound.Outside

            var m_Radius = tv.Field("m_Radius").GetValue<float>();
            var m_Height = tv.Field("m_Height").GetValue<float>();
            var m_Center = tv.Field("m_Center").GetValue<Vector3>();
            var radius = m_Radius * Mathf.Abs(obj.transform.lossyScale.z);
            var height = (m_Height - m_Radius) * 0.5f;
            if (height <= 0f)
            {
                Popcron.Gizmos.Sphere(obj.transform.TransformPoint(m_Center), radius, color);
                return;
            }
            var center = m_Center;
            var center2 = m_Center;
            var m_Direction = (int)tv.Field("m_Direction").GetValue(); // Direction enum
            switch (m_Direction)
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
                Popcron.Gizmos.Sphere(p1, radius, Color.blue);
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

            Popcron.Gizmos.Arc(p1, radius, p1Rotation * Quaternion.Euler(90, 0, 0), 0f, 180f, color);
            Popcron.Gizmos.Arc(p1, radius, p1Rotation * Quaternion.Euler(0, 90, 0), 90f, 180f, color);
            Popcron.Gizmos.Circle(p1, radius, p1Rotation, color);
            // Second side
            Popcron.Gizmos.Arc(p2, radius, p2Rotation * Quaternion.Euler(90, 0, 0), 0f, 180f, color);
            Popcron.Gizmos.Arc(p2, radius, p2Rotation * Quaternion.Euler(0, 90, 0), 90f, 180f, color);
            Popcron.Gizmos.Circle(p2, radius, p2Rotation, color);
            // Lines
            Popcron.Gizmos.Line(p1 + p1Rotation * Vector3.down * radius, p2 + p2Rotation * Vector3.down * radius, color);
            Popcron.Gizmos.Line(p1 + p1Rotation * Vector3.left * radius, p2 + p2Rotation * Vector3.right * radius, color);
            Popcron.Gizmos.Line(p1 + p1Rotation * Vector3.up * radius, p2 + p2Rotation * Vector3.up * radius, color);
            Popcron.Gizmos.Line(p1 + p1Rotation * Vector3.right * radius, p2 + p2Rotation * Vector3.left * radius, color);
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
