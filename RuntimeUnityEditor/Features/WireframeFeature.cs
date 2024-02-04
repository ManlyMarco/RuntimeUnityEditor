using System.Linq;
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
        private static readonly Dictionary<Camera, CameraWithWireframe> _targets = new Dictionary<Camera, CameraWithWireframe>();
        
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
                        Camera.onPreRender += OnPreRender;
                        Camera.onPostRender += OnPostRender;
                    }
                    else
                    {
                        Camera.onPreRender -= OnPreRender;
                        Camera.onPostRender -= OnPostRender;
                        GL.wireframe = false;

                        foreach (var cam in _targets)
                            cam.Value.Restore();

                        _targets.Clear();
                    }
                }
            }
        }

        class CameraWithWireframe
        {
            //Incompatible post-effects
            static string[] _DisableEffectNames = { "GlobalFog" };

            Camera _camera;
            Behaviour[] _disableEffects;

            public CameraWithWireframe( Camera camera )
            {
                _camera = camera;
                _disableEffects = _DisableEffectNames.Select(name => camera.gameObject.GetComponent(name) as Behaviour).Where(c => c != null && c.enabled).ToArray();
            }

            public void Set()
            {
                if (_camera == null)
                    return;

                GL.wireframe = true;
                foreach (var effect in _disableEffects)
                    if(effect != null)
                        effect.enabled = false;
            }

            public void Restore()
            {
                if (_camera == null)
                    return;

                foreach (var effect in _disableEffects)
                    if (effect != null)
                        effect.enabled = true;
                GL.wireframe = false;
            }
        }

        private static void OnPreRender(Camera cam)
        {
            // Avoid affecting game state if wireframe is already used
            if (GL.wireframe) return;

            if( !_targets.TryGetValue(cam, out var wireframe) )
                wireframe = _targets[cam] = new CameraWithWireframe(cam);

            wireframe.Set();
        }

        private static void OnPostRender(Camera cam)
        {
            if (_targets.TryGetValue(cam, out var wireframe))
                wireframe.Restore();
        }
    }
}
