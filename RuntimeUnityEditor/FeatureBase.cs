using System;

namespace RuntimeUnityEditor.Core
{
    internal interface IFeature
    {
        bool Enabled { get; set; }
        void OnInitialize(RuntimeUnityEditorCore.InitSettings initSettings);
        void OnUpdate();
        void OnLateUpdate();
        void OnOnGUI();
        void OnVisibleChanged(bool visible);
    }

    public abstract class FeatureBase<T> : IFeature where T : FeatureBase<T>
    {
        // ReSharper disable once StaticMemberInGenericType
        private static bool _initialized;
        public static bool Initialized => _initialized;
        public static T Instance { get; private set; }

        protected FeatureBase()
        {
            FeatureBase<T>.Instance = (T)this;
        }

        public virtual bool Enabled { get; set; }

        void IFeature.OnInitialize(RuntimeUnityEditorCore.InitSettings initSettings)
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
        void IFeature.OnVisibleChanged(bool visible)
        {
            if (_initialized && Enabled)
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
        }

        protected abstract void Initialize(RuntimeUnityEditorCore.InitSettings initSettings);
        protected virtual void Update() { }
        protected virtual void LateUpdate() { }
        protected virtual void OnGUI() { }
        protected virtual void VisibleChanged(bool visible) { }
    }
}
