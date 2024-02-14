using System.Linq;
using System.Collections;
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
        // Effects incompatible with wireframes
        private static readonly string[] _disableEffectNames = { "GlobalFog", "BloomAndFlares", "CustomRender", "AmplifyColorEffect", "PostProcessLayer" };

        private MonoBehaviour _monoBehaviour;

        private readonly List<Behaviour> _disabledEffects = new List<Behaviour>();
        private static Camera _targetCamera;
        private bool _updateCoRunning;

        protected override void Initialize(InitSettings initSettings)
        {
            DisplayName = "Wireframe";
            Enabled = false;

            _monoBehaviour = initSettings.PluginMonoBehaviour;
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
                        _monoBehaviour.StartCoroutine(UpdateCo());
                }
            }
        }

        private IEnumerator UpdateCo()
        {
            // Prevent race condition when rapidly toggling Enabled
            if(_updateCoRunning) 
                yield break;

            _updateCoRunning = true;

            CollectEffects();
            SetEffectEnabled(false);

            yield return null;

            // Need to wait for multiple frames for some effects to be disabled
            if (Enabled)
                yield return null;

            if (Enabled)
            {
                Camera.onPreRender += OnPreRender;
                Camera.onPostRender += OnPostRender;

                yield return null;

                while (Enabled)
                {
                    // Turn effects off every frame in case they are re-enabled
                    SetEffectEnabled(false);
                    yield return null;
                }

                Camera.onPreRender -= OnPreRender;
                Camera.onPostRender -= OnPostRender;
            }

            SetEffectEnabled(true);

            _updateCoRunning = false;
        }

        private void CollectEffects()
        {
            _disabledEffects.Clear();
            // Find all cameras and their problematic effects that are currently enabled
            _disabledEffects.AddRange(Object.FindObjectsOfType<Camera>()
                                            .SelectMany(cam => _disableEffectNames.Select(cam.GetComponent))
                                            .OfType<Behaviour>()
                                            .Where(b => b && b.enabled));
        }

        private void SetEffectEnabled(bool enabled)
        {
            for (var i = 0; i < _disabledEffects.Count; i++)
            {
                var effect = _disabledEffects[i];
                if (effect)
                    effect.enabled = enabled;
            }
        }

        private static void OnPreRender(Camera cam)
        {
            if (GL.wireframe) return;

            _targetCamera = cam;
            GL.wireframe = true;
        }

        private static void OnPostRender(Camera cam)
        {
            if (_targetCamera == cam)
            {
                GL.wireframe = false;
                _targetCamera = null;
            }
        }
    }
}
