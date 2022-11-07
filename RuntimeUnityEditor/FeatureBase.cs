using System;
using RuntimeUnityEditor.Core.Utils;
using RuntimeUnityEditor.Core.Utils.Abstractions;

namespace RuntimeUnityEditor.Core
{
    public interface IFeature
    {
        bool Enabled { get; set; }
        //bool Visible { get; }
        void OnInitialize(InitSettings initSettings);
        void OnUpdate();
        void OnLateUpdate();
        void OnOnGUI();
        void OnEditorShownChanged(bool visible);
        FeatureDisplayType DisplayType { get; }
        string DisplayName { get; }
    }

    public enum FeatureDisplayType
    {
        Hidden,
        Feature,
        Window
    }

    public abstract class FeatureBase<T> : IFeature where T : FeatureBase<T>
    {
        // ReSharper disable once StaticMemberInGenericType
        private static bool _initialized;
        public static bool Initialized => _initialized;
        public static T Instance { get; private set; }

        protected FeatureBase()
        {
            DisplayType = FeatureDisplayType.Feature;
            FeatureBase<T>.Instance = (T)this;
        }

        private protected string _displayName;
        private protected bool _enabled;

        /// <summary>
        /// Name shown in taskbar
        /// </summary>
        public virtual string DisplayName
        {
            get => _displayName ?? (_displayName = GetType().Name);
            set => _displayName = value;
        }

        /// <summary>
        /// If this instance is enabled and can be shown (if RUE is shown as a whole).
        /// </summary>
        public virtual bool Enabled
        {
            get => _enabled;
            set
            {
                if(_enabled != value)
                {
                    var prevVisible = Visible;
                    _enabled = value;
                    var nowVisible = Visible;
                    if (prevVisible != nowVisible)
                        OnVisibleChanged(nowVisible);
                }
            }
        }

        /// <summary>
        /// If this instance is actually shown on screen / has its events fired.
        /// </summary>
        public bool Visible => Enabled && RuntimeUnityEditorCore.Instance.Show;

        /// <summary>
        /// How this Feature is shown in taskbar
        /// </summary>
        public FeatureDisplayType DisplayType { get; protected set; }

        void IFeature.OnInitialize(InitSettings initSettings)
        {
            if (Initialized) throw new InvalidOperationException("The Feature is already initialized");

            Initialize(initSettings);
            _initialized = true;
        }

        void IFeature.OnUpdate()
        {
            if (_initialized && Enabled)
            {
                try
                {
                    Update();
                }
                catch (Exception e)
                {
                    RuntimeUnityEditorCore.Logger.Log(LogLevel.Error, e);
                }
            }
        }
        void IFeature.OnLateUpdate()
        {
            if (_initialized && Enabled)
            {
                try
                {
                    LateUpdate();
                }
                catch (Exception e)
                {
                    RuntimeUnityEditorCore.Logger.Log(LogLevel.Error, e);
                }
            }
        }
        void IFeature.OnOnGUI()
        {
            if (_initialized && Enabled)
            {
                try
                {
                    OnGUI();
                }
                catch (Exception e)
                {
                    RuntimeUnityEditorCore.Logger.Log(LogLevel.Error, e);
                }
            }
        }
        void IFeature.OnEditorShownChanged(bool visible)
        {
            if (_initialized)
            {
                if (!Enabled) return;

                OnVisibleChanged(visible);
            }
        }

        protected virtual void OnVisibleChanged(bool visible)
        {
            try
            {
                VisibleChanged(visible);
            }
            catch (Exception e)
            {
                RuntimeUnityEditorCore.Logger.Log(LogLevel.Error, e);
            }
        }

        protected abstract void Initialize(InitSettings initSettings);
        protected virtual void Update() { }
        protected virtual void LateUpdate() { }
        protected virtual void OnGUI() { }
        protected virtual void VisibleChanged(bool visible) { }
    }
}
