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
        private static Camera _targetCamera = null;
        private MonoBehaviour _monoBehaviour;

        //Effects incompatible with wireframes.
        private static string[] _DisableEffectNames = { "GlobalFog", "BloomAndFlares", "CustomRender", "AmplifyColorEffect", "PostProcessLayer" };

        private List<Behaviour> _disabledEffects = new List<Behaviour>();

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
                    {
                        _monoBehaviour.StartCoroutine(Routine());
                    }
                }
            }
        }

        IEnumerator Routine()
        {
            SetEffectEnabled(false, true);
            yield return null;

            if (Enabled)
                yield return null;  //Wait for multiple frames

            if (Enabled)
            {
                Camera.onPreRender += OnPreRender;
                Camera.onPostRender += OnPostRender;
                yield return null;

                while (Enabled)
                {
                    SetEffectEnabled(false, false); //Always off
                    yield return null;
                }

                Camera.onPreRender -= OnPreRender;
                Camera.onPostRender -= OnPostRender;
            }

            SetEffectEnabled(true, false);
        }

        private void SetEffectEnabled( bool enabled, bool collectEffects )
        {
            if( collectEffects)
            {
                _disabledEffects.Clear();

                foreach (var camera in GameObject.FindObjectsOfType<Camera>())
                {
                    _disabledEffects.AddRange(
                        _DisableEffectNames
                            .Select(name => camera.gameObject.GetComponent(name) as Behaviour)
                            .Where(c => c != null && c.enabled)
                        );
                }
            }

            foreach (var effect in _disabledEffects)
                if (effect != null)
                    effect.enabled = enabled;
        }

        private static void OnPreRender(Camera cam)
        {
            // Avoid affecting game state if wireframe is already used
            if (GL.wireframe) return;

            _targetCamera = cam;
            GL.wireframe = true;
        }

        private static void OnPostRender(Camera cam)
        {
            if( _targetCamera == cam )
            {
                GL.wireframe = false;
                _targetCamera = null;
            }
        }
    }
}
