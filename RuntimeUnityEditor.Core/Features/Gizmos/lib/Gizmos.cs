using System;
using RuntimeUnityEditor.Core.Gizmos.lib.Drawers;
using UnityEngine;

namespace RuntimeUnityEditor.Core.Gizmos.lib
{
    /// <summary>
    /// Taken from https://github.com/popcron/gizmos
    /// </summary>
    public class Gizmos
    {
        private static string _prefsKey = null;
        private static int? _bufferSize = null;
        private static bool? _enabled = null;
        private static float? _dashGap = null;
        private static bool? _cull = null;
        private static int? _pass = null;
        private static Vector3? _offset = null;

        private static Vector3[] buffer = new Vector3[BufferSize];

        /// <summary>
        /// By default, it will always render to scene view camera and the main camera.
        /// Subscribing to this allows you to whitelist your custom cameras.
        /// </summary>
        public static Func<Camera, bool> CameraFilter = cam => false;

        private static string PrefsKey
        {
            get
            {
                if (string.IsNullOrEmpty(_prefsKey))
                {
                    _prefsKey = $"{SystemInfo.deviceUniqueIdentifier}.{Application.companyName}.{Application.productName}.{Constants.UniqueIdentifier}";
                }

                return _prefsKey;
            }
        }

        /// <summary>
        /// The size of the total gizmos buffer.
        /// Default is 4096.
        /// </summary>
        public static int BufferSize
        {
            get
            {
                if (_bufferSize == null)
                {
                    //todo use config
                    //_bufferSize = PlayerPrefs.GetInt($"{PrefsKey}.BufferSize", 4096);
                    _bufferSize = 4096;
                }

                return _bufferSize.Value;
            }
            set
            {
                value = Mathf.Clamp(value, 0, int.MaxValue);
                if (_bufferSize != value)
                {
                    _bufferSize = value;
                    //todo use config
                    //PlayerPrefs.SetInt($"{PrefsKey}.BufferSize", value);

                    //buffer size changed, so recreate the buffer array too
                    buffer = new Vector3[value];
                }
            }
        }

        /// <summary>
        /// Toggles wether the gizmos could be drawn or not.
        /// </summary>
        public static bool Enabled
        {
            get
            {
                if (_enabled == null)
                {
                    //todo use config
                    //_enabled = PlayerPrefs.GetInt($"{PrefsKey}.Enabled", 1) == 1;
                    _enabled = true;
                }

                return _enabled.Value;
            }
            set
            {
                //if (_enabled != value)
                {
                    _enabled = value;
                    //todo use config
                    //PlayerPrefs.SetInt($"{PrefsKey}.Enabled", value ? 1 : 0);
                }
            }
        }

        /// <summary>
        /// The size of the gap when drawing dashed elements.
        /// Default gap size is 0.1
        /// </summary>
        public static float DashGap
        {
            get
            {
                if (_dashGap == null)
                {
                    //todo use config
                    //_dashGap = PlayerPrefs.GetFloat($"{PrefsKey}.DashGap", 0.1f);
                    _dashGap = 0.1f;
                }

                return _dashGap.Value;
            }
            set
            {
                if (_dashGap != value)
                {
                    _dashGap = value;
                    //todo use config
                    //PlayerPrefs.SetFloat($"{PrefsKey}.DashGap", value);
                }
            }
        }

        /// <summary>
        /// Should the camera not draw elements that are not visible?
        /// </summary>
        public static bool FrustumCulling
        {
            get
            {
                if (_cull == null)
                {
                    //todo use config
                    //_cull = PlayerPrefs.GetInt($"{PrefsKey}.FrustumCulling", 1) == 1;
                    _cull = true;
                }

                return _cull.Value;
            }
            set
            {
                if (_cull != value)
                {
                    _cull = value;
                    //todo use config
                    //PlayerPrefs.SetInt($"{PrefsKey}.FrustumCulling", value ? 1 : 0);
                }
            }
        }

        /// <summary>
        /// The material being used to render.
        /// </summary>
        public static Material Material
        {
            get => GizmosInstance.Material;
            set => GizmosInstance.Material = value;
        }

        /// <summary>
        /// Rendering pass to activate.
        /// </summary>
        public static int Pass
        {
            get
            {
                if (_pass == null)
                {
                    //todo use config
                    //_pass = PlayerPrefs.GetInt($"{PrefsKey}.Pass", 0);
                    _pass = 0;
                }

                return _pass.Value;
            }
            set
            {
                if (_pass != value)
                {
                    _pass = value;
                    //todo use config
                    //PlayerPrefs.SetInt($"{PrefsKey}.Pass", value);
                }
            }
        }

        /// <summary>
        /// Global offset for all points. Default is (0, 0, 0).
        /// </summary>
        public static Vector3 Offset
        {
            get
            {
                //const string Delim = ",";
                if (_offset == null)
                {
                    //todo use config
                    //string data = PlayerPrefs.GetString($"{PrefsKey}.Offset", 0 + Delim + 0 + Delim + 0);
                    //int indexOf = data.IndexOf(Delim);
                    //int lastIndexOf = data.LastIndexOf(Delim);
                    //if (indexOf + lastIndexOf > 0)
                    //{
                    //    string[] arr = data.Split(Delim[0]);
                    //    _offset = new Vector3(float.Parse(arr[0]), float.Parse(arr[1]), float.Parse(arr[2]));
                    //}
                    //else
                    //{
                    //    return Vector3.zero;
                    //}
                    _offset = Vector3.zero;
                }

                return _offset.Value;
            }
            set
            {
                //const string Delim = ",";
                //if (_offset != value)
                {
                    _offset = value;
                    //todo use config
                    //PlayerPrefs.SetString($"{PrefsKey}.Offset", value.x + Delim + value.y + Delim + value.y);
                }
            }
        }

        /// <summary>
        /// Draws an element onto the screen.
        /// </summary>
        public static void Draw<T>(Color? color, bool dashed, params object[] args) where T : Drawer
        {
            if (!Enabled)
            {
                return;
            }

            Drawer drawer = Drawer.Get<T>();
            if (drawer != null)
            {
                int points = drawer.Draw(ref buffer, args);

                //copy from buffer and add to the queue
                Vector3[] array = new Vector3[points];
                Array.Copy(buffer, array, points);
                GizmosInstance.Submit(array, color, dashed);
            }
        }

        /// <summary>
        /// Draws an array of lines. Useful for things like paths.
        /// </summary>
        public static void Lines(Vector3[] lines, Color? color = null, bool dashed = false)
        {
            if (!Enabled)
            {
                return;
            }

            GizmosInstance.Submit(lines, color, dashed);
        }

        /// <summary>
        /// Draw line in world space.
        /// </summary>
        public static void Line(Vector3 a, Vector3 b, Color? color = null, bool dashed = false)
        {
            Draw<LineDrawer>(color, dashed, a, b);
        }

        /// <summary>
        /// Draw square in world space.
        /// </summary>
        public static void Square(Vector2 position, Vector2 size, Color? color = null, bool dashed = false)
        {
            Square(position, Quaternion.identity, size, color, dashed);
        }

        /// <summary>
        /// Draw square in world space with float diameter parameter.
        /// </summary>
        public static void Square(Vector2 position, float diameter, Color? color = null, bool dashed = false)
        {
            Square(position, Quaternion.identity, Vector2.one * diameter, color, dashed);
        }

        /// <summary>
        /// Draw square in world space with a rotation parameter.
        /// </summary>
        public static void Square(Vector2 position, Quaternion rotation, Vector2 size, Color? color = null, bool dashed = false)
        {
            Draw<SquareDrawer>(color, dashed, position, rotation, size);
        }

        /// <summary>
        /// Draws a cube in world space.
        /// </summary>
        public static void Cube(Vector3 position, Quaternion rotation, Vector3 size, Color? color = null, bool dashed = false)
        {
            Draw<CubeDrawer>(color, dashed, position, rotation, size);
        }

        /// <summary>
        /// Draws a rectangle in screen space.
        /// </summary>
        public static void Rect(Rect rect, Camera camera, Color? color = null, bool dashed = false)
        {
            rect.y = Screen.height - rect.y;
            Vector2 corner = camera.ScreenToWorldPoint(new Vector2(rect.x, rect.y - rect.height));
            Draw<SquareDrawer>(color, dashed, corner + rect.size * 0.5f, Quaternion.identity, rect.size);
        }

        /// <summary>
        /// Draws a representation of a bounding box.
        /// </summary>
        public static void Bounds(Bounds bounds, Color? color = null, bool dashed = false)
        {
            Draw<CubeDrawer>(color, dashed, bounds.center, Quaternion.identity, bounds.size);
        }

        /// <summary>
        /// Draws a cone similar to the one that spot lights draw.
        /// </summary>
        public static void Cone(Vector3 position, Quaternion rotation, float length, float angle, Color? color = null, bool dashed = false, int pointsCount = 16)
        {
            //draw the end of the cone
            float endAngle = Mathf.Tan(angle * 0.5f * Mathf.Deg2Rad) * length;
            Vector3 forward = rotation * Vector3.forward;
            Vector3 endPosition = position + forward * length;
            float offset = 0f;
            Draw<PolygonDrawer>(color, dashed, endPosition, pointsCount, endAngle, offset, rotation);

            //draw the 4 lines
            for (int i = 0; i < 4; i++)
            {
                float a = i * 90f * Mathf.Deg2Rad;
                Vector3 point = rotation * new Vector3(Mathf.Cos(a), Mathf.Sin(a)) * endAngle;
                Line(position, position + point + forward * length, color, dashed);
            }
        }

        /// <summary>
        /// Draws a sphere at position with specified radius.
        /// </summary>
        public static void Sphere(Vector3 position, float radius, Color? color = null, bool dashed = false, int pointsCount = 16)
        {
            float offset = 0f;
            Draw<PolygonDrawer>(color, dashed, position, pointsCount, radius, offset, Quaternion.Euler(0f, 0f, 0f));
            Draw<PolygonDrawer>(color, dashed, position, pointsCount, radius, offset, Quaternion.Euler(90f, 0f, 0f));
            Draw<PolygonDrawer>(color, dashed, position, pointsCount, radius, offset, Quaternion.Euler(0f, 90f, 90f));
        }

        /// <summary>
        /// Draws a circle in world space and billboards towards the camera.
        /// </summary>
        public static void Circle(Vector3 position, float radius, Camera camera, Color? color = null, bool dashed = false, int pointsCount = 16)
        {
            float offset = 0f;
            Quaternion rotation = Quaternion.LookRotation(position - camera.transform.position);
            Draw<PolygonDrawer>(color, dashed, position, pointsCount, radius, offset, rotation);
        }

        /// <summary>
        /// Draws a circle in world space with a specified rotation.
        /// </summary>
        public static void Circle(Vector3 position, float radius, Quaternion rotation, Color? color = null, bool dashed = false, int pointsCount = 16)
        {
            float offset = 0f;
            Draw<PolygonDrawer>(color, dashed, position, pointsCount, radius, offset, rotation);
        }

        /// <summary>
        /// Draws an arc in world space.
        /// </summary>
        public static void Arc(Vector3 position, float radius, Quaternion rotation, float offset, float drawnAngle, Color? color = null, bool dashed = false, int pointsCount = 16)
        {
            Draw<ArcDrawer>(color, dashed, position, pointsCount, radius, offset, rotation, drawnAngle);
        }
    }
}
