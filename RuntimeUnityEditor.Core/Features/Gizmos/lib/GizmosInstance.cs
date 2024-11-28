using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

namespace RuntimeUnityEditor.Core.Gizmos.lib
{
    /// <summary>
    /// Taken from https://github.com/popcron/gizmos
    /// </summary>
    //[ExecuteInEditMode]
    //[AddComponentMenu("")]
    public class GizmosInstance : MonoBehaviour
    {
        private const int DefaultQueueSize = 4096;

        private static GizmosInstance instance;
        private static bool hotReloaded = true;
        private static Material defaultMaterial;
        private static Plane[] cameraPlanes = new Plane[6];

        private Material overrideMaterial;
        private int queueIndex = 0;
        private int lastFrame;
        private Element[] queue = new Element[DefaultQueueSize];

        /// <summary>
        /// The material being used to render
        /// </summary>
        public static Material Material
        {
            get
            {
                GizmosInstance inst = GetOrCreate();
                if (inst.overrideMaterial)
                {
                    return inst.overrideMaterial;
                }

                return DefaultMaterial;
            }
            set
            {
                GizmosInstance inst = GetOrCreate();
                inst.overrideMaterial = value;
            }
        }

        /// <summary>
        /// The default line renderer material
        /// </summary>
        public static Material DefaultMaterial
        {
            get
            {
                if (!defaultMaterial)
                {
                    // Unity has a built-in shader that is useful for drawing
                    // simple colored things.
                    Shader shader = Shader.Find("Hidden/Internal-Colored");
                    defaultMaterial = new Material(shader)
                    {
                        hideFlags = HideFlags.HideAndDontSave
                    };

                    // Turn on alpha blending
                    defaultMaterial.SetInt("_SrcBlend", (int)BlendMode.SrcAlpha);
                    defaultMaterial.SetInt("_DstBlend", (int)BlendMode.OneMinusSrcAlpha);
                    defaultMaterial.SetInt("_Cull", (int)CullMode.Off);
                    defaultMaterial.SetInt("_ZWrite", 0);
                    defaultMaterial.SetInt("_ZTest", (int)CompareFunction.Always);
                }

                return defaultMaterial;
            }
        }

        internal static GizmosInstance GetOrCreate()
        {
            if (hotReloaded || !instance)
            {
                GizmosInstance[] gizmosInstances = FindObjectsOfType<GizmosInstance>();
                for (int i = 0; i < gizmosInstances.Length; i++)
                {
                    if (i == 0)
                        instance = gizmosInstances[i];
                    else
                    {
                        //destroy any extra gizmo instances
                        if (Application.isPlaying)
                        {
                            Destroy(gizmosInstances[i]);
                        }
                        else
                        {
                            DestroyImmediate(gizmosInstances[i]);
                        }
                    }
                }

                //none were found, create a new one
                if (!instance)
                {
                    instance = new GameObject(typeof(GizmosInstance).FullName).AddComponent<GizmosInstance>();
                    instance.gameObject.hideFlags = HideFlags.HideInHierarchy | HideFlags.HideInInspector;
                }

                hotReloaded = false;
            }

            return instance;
        }

        private float CurrentTime
        {
            get
            {
                float time = 0f;
                if (Application.isPlaying)
                {
                    time = Time.time;
                }
                else
                {
#if UNITY_EDITOR
                    time = (float)EditorApplication.timeSinceStartup;
#endif
                }

                return time;
            }
        }

        /// <summary>
        /// Submits an array of points to draw into the queue.
        /// </summary>
        internal static void Submit(Vector3[] points, Color? color, bool dashed)
        {
            GizmosInstance inst = GetOrCreate();

            //if new frame, reset index
            if (inst.lastFrame != Time.frameCount)
            {
                inst.lastFrame = Time.frameCount;
                inst.queueIndex = 0;
            }

            //excedeed the length, so make it even bigger
            if (inst.queueIndex >= inst.queue.Length)
            {
                Element[] bigger = new Element[inst.queue.Length + DefaultQueueSize];
                for (int i = inst.queue.Length; i < bigger.Length; i++)
                {
                    bigger[i] = new Element();
                }

                Array.Copy(inst.queue, 0, bigger, 0, inst.queue.Length);
                inst.queue = bigger;
            }

            inst.queue[inst.queueIndex].color = color ?? Color.white;
            inst.queue[inst.queueIndex].points = points;
            inst.queue[inst.queueIndex].dashed = dashed;

            inst.queueIndex++;
        }

        private void OnEnable()
        {
            //populate queue with empty elements
            queue = new Element[DefaultQueueSize];
            for (int i = 0; i < DefaultQueueSize; i++)
            {
                queue[i] = new Element();
            }

#if IL2CPP
            if (GraphicsSettings.renderPipelineAsset == null)
                Camera.onPostRender += (Camera.CameraCallback)OnRendered;
            else
                RenderPipelineManager.endCameraRendering += (Il2CppSystem.Action<ScriptableRenderContext, Camera>)OnRendered2;
#else
            //todo better handle old mono versions which don't have renderPipelineAsset etc.
            Camera.onPostRender += OnRendered;
#endif
        }

#if IL2CPP
        private void OnRendered2(ScriptableRenderContext context, Camera camera)
        {
            OnRendered(camera);
        }
#endif

        private void OnDisable()
        {
#if IL2CPP
            if (GraphicsSettings.renderPipelineAsset == null)
                Camera.onPostRender -= (Camera.CameraCallback)OnRendered;
            else
                RenderPipelineManager.endCameraRendering -= (Il2CppSystem.Action<ScriptableRenderContext, Camera>)OnRendered2;
#else
            //todo better handle old mono versions which don't have renderPipelineAsset etc.
            Camera.onPostRender -= OnRendered;
#endif
        }

        //private void OnRendered(ScriptableRenderContext context, Camera camera) => OnRendered(camera);

        private bool ShouldRenderCamera(Camera camera)
        {
            if (!camera)
            {
                return false;
            }

            //allow the scene and main camera always
            bool isSceneCamera = false;
#if UNITY_EDITOR
            SceneView sceneView = SceneView.currentDrawingSceneView;
            if (sceneView == null)
            {
                sceneView = SceneView.lastActiveSceneView;
            }

            if (sceneView != null && sceneView.camera == camera)
            {
                isSceneCamera = true;
            }
#endif
            if (isSceneCamera || camera.CompareTag("MainCamera"))
            {
                return true;
            }

            //it passed through the filter
            if (Gizmos.CameraFilter?.Invoke(camera) == true)
            {
                return true;
            }

            return false;
        }

        private bool IsVisibleByCamera(Element points, Camera camera)
        {
            if (!camera)
            {
                return false;
            }

            //essentially check if at least 1 point is visible by the camera
            for (int i = 0; i < points.points.Length; i++)
            {
                Vector3 vp = camera.WorldToViewportPoint(points.points[i]/*, camera.stereoActiveEye*/);
                if (vp.x >= 0 && vp.x <= 1 && vp.y >= 0 && vp.y <= 1)
                {
                    return true;
                }
            }

            return false;
        }

        private void Update()
        {
            //always render something
            //Gizmos.Line(default, default);
        }

        private void OnRendered(Camera camera)
        {
            //shouldnt be rendering
            if (!Gizmos.Enabled)
            {
                queueIndex = 0;
                return;
            }

            //check if this camera is ok to render with
            if (!ShouldRenderCamera(camera))
            {
                //GL.PushMatrix();
                //GL.Begin(GL.LINES);
                //
                ////bla bla bla
                //
                //GL.End();
                //GL.PopMatrix();
                return;
            }

            Material.SetPass(Gizmos.Pass);

            Vector3 offset = Gizmos.Offset;

            GL.PushMatrix();
            GL.MultMatrix(Matrix4x4.identity);
            GL.Begin(1); // 1 - GL.LINES

            bool alt = CurrentTime % 1 > 0.5f;
            float dashGap = Mathf.Clamp(Gizmos.DashGap, 0.01f, 32f);
            bool frustumCull = Gizmos.FrustumCulling;
            List<Vector3> points = new List<Vector3>();

            //draw le elements
            for (int e = 0; e < queueIndex; e++)
            {
                //just in case
                if (queue.Length <= e)
                {
                    break;
                }

                Element element = queue[e];

                //dont render this thingy if its not inside the frustum
                if (frustumCull)
                {
                    if (!IsVisibleByCamera(element, camera))
                    {
                        continue;
                    }
                }

                points.Clear();
                if (element.dashed)
                {
                    //subdivide
                    for (int i = 0; i < element.points.Length - 1; i++)
                    {
                        Vector3 pointA = element.points[i];
                        Vector3 pointB = element.points[i + 1];
                        Vector3 direction = pointB - pointA;
                        if (direction.sqrMagnitude > dashGap * dashGap * 2f)
                        {
                            float magnitude = direction.magnitude;
                            int amount = Mathf.RoundToInt(magnitude / dashGap);
                            direction /= magnitude;

                            for (int p = 0; p < amount - 1; p++)
                            {
                                if (p % 2 == (alt ? 1 : 0))
                                {
                                    float startLerp = p / (amount - 1f);
                                    float endLerp = (p + 1) / (amount - 1f);
                                    Vector3 start = Vector3.Lerp(pointA, pointB, startLerp);
                                    Vector3 end = Vector3.Lerp(pointA, pointB, endLerp);
                                    points.Add(start);
                                    points.Add(end);
                                }
                            }
                        }
                        else
                        {
                            points.Add(pointA);
                            points.Add(pointB);
                        }
                    }
                }
                else
                {
                    points.AddRange(element.points);
                }

                GL.Color(element.color);
                for (int i = 0; i < points.Count; i++)
                {
                    GL.Vertex(points[i] + offset);
                }
            }

            GL.End();
            GL.PopMatrix();
        }
    }
}
