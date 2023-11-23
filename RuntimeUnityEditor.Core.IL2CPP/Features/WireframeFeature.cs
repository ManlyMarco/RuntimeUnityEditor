using System.Collections.Generic;
using RuntimeUnityEditor.Core.Utils.Abstractions;
using UnityEngine;
#pragma warning disable CS1591

namespace RuntimeUnityEditor.Core
{
    /// <summary>
    /// Feature that turns on Unity's built-in wireframe mode.
    /// </summary>
    public sealed class WireframeFeature : FeatureBase<WireframeFeature>
    {
        private static readonly Dictionary<Camera, CameraClearFlags> _origFlags = new Dictionary<Camera, CameraClearFlags>();
        
        protected override void Initialize(InitSettings initSettings)
        {
            DisplayName = "Wireframe";
            Enabled = false;
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
                        Camera.onPreRender += (Camera.CameraCallback)OnPreRender;
                        Camera.onPostRender += (Camera.CameraCallback)OnPostRender;
                    }
                    else
                    {
                        Camera.onPreRender -= (Camera.CameraCallback)OnPreRender;
                        Camera.onPostRender -= (Camera.CameraCallback)OnPostRender;
                        GL.wireframe = false;

                        foreach (var origFlag in _origFlags)
                        {
                            if (origFlag.Key != null)
                                origFlag.Key.clearFlags = origFlag.Value;
                        }
                        _origFlags.Clear();
                    }
                }
            }
        }

        private static void OnPreRender(Camera cam)
        {
            // Avoid affecting game state if wireframe is already used
            if (GL.wireframe) return;

            if (!_origFlags.ContainsKey(cam))
                _origFlags.Add(cam, cam.clearFlags);

            cam.clearFlags = CameraClearFlags.Color;
            GL.wireframe = true;
        }

        private static void OnPostRender(Camera cam)
        {
            if (_origFlags.TryGetValue(cam, out var flags))
            {
                cam.clearFlags = flags;
                GL.wireframe = false;
            }
        }
    }
}
