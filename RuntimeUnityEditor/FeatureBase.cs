using System;
using RuntimeUnityEditor.Core.Utils;
using RuntimeUnityEditor.Core.Utils.Abstractions;

namespace RuntimeUnityEditor.Core
{
    /// <summary>
    /// Feature for use with RuntimeUnityEditor. Custom features can be added with <see cref="RuntimeUnityEditorCore.AddFeature"/>.
    /// Consider using <see cref="FeatureBase{T}"/> or <see cref="Window{T}"/> instead of the bare interface.
    /// </summary>
    public interface IFeature
    {
        /// <summary>
        /// Turn on this feature's functionality.
        /// </summary>
        bool Enabled { get; set; }
        /// <summary>
        /// Initialize this feature. If this throws, the feature will be skipped.
        /// </summary>
        void OnInitialize(InitSettings initSettings);
        /// <summary>
        /// Unity Update callback.
        /// </summary>
        void OnUpdate();
        /// <summary>
        /// Unity LateUpdate callback.
        /// </summary>
        void OnLateUpdate();
        /// <summary>
        /// Unity OnGUI callback.
        /// </summary>
        void OnOnGUI();
        /// <summary>
        /// Callback for RuntimeUnityEditor being toggled shown/hidden. If not visible, Unity callbacks are not called.
        /// </summary>
        void OnEditorShownChanged(bool visible);
        /// <summary>
        /// How this feature appears in the UI.
        /// </summary>
        FeatureDisplayType DisplayType { get; }
        string DisplayName { get; }
    }

    /// <summary>
    /// Controls how a feature appears in the RuntimeUnityEditor interface.
    /// </summary>
    public enum FeatureDisplayType
    {
        /// <summary>
        /// Do not show on the taskbar, <see cref="IFeature.Enabled"/> has to be manually set in that case.
        /// </summary>
        Hidden,
        /// <summary>
        /// Show as a taskbar toggle together with other features.
        /// </summary>
        Feature,
        /// <summary>
        /// Show as a taskbar button together with other windows.
        /// </summary>
        Window
    }

    /// <summary>
    /// Base implementation of <see cref="IFeature"/>.
    /// <typeparamref name="T"/> should be your derived class's Type, e.g. <code>public class MyFeature : FeatureBase&lt;MyFeature&gt;</code>.
    /// </summary>
    public abstract class FeatureBase<T> : IFeature where T : FeatureBase<T>
    {
        // ReSharper disable once StaticMemberInGenericType
        private static bool _initialized;
        /// <summary>
        /// True if this feature was successfully initialized.
        /// </summary>
        public static bool Initialized => _initialized;
        /// <summary>
        /// Instance of this feature (null if not initialized).
        /// </summary>
        public static T Instance { get; private set; }

        protected FeatureBase()
        {
            DisplayType = FeatureDisplayType.Feature;
            FeatureBase<T>.Instance = (T)this;
        }

        protected string SettingCategory = "Features";
        private protected string _displayName;
        private bool _enabled;
        private Action<bool> _confEnabled;

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
                if (_enabled != value)
                {
                    // Need to get this before setting _enabled
                    var prevVisible = Visible;
                    _enabled = value;

                    var nowVisible = Visible;
                    if (prevVisible != nowVisible)
                        OnVisibleChanged(nowVisible);

                    _confEnabled?.Invoke(value);
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
            AfterInitialized(initSettings);
            _initialized = true;
        }

        /// <summary>
        /// Runs after <see cref="Initialize"/> has successfully finished. Must succeed for the feature to be considered initialized.
        /// </summary>
        protected virtual void AfterInitialized(InitSettings initSettings)
        {
            _confEnabled = initSettings.RegisterSetting(SettingCategory, DisplayName + " enabled", Enabled, string.Empty, b => Enabled = b);
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

        /// <inheritdoc cref="IFeature.OnInitialize"/>
        protected abstract void Initialize(InitSettings initSettings);
        /// <inheritdoc cref="IFeature.OnUpdate"/>
        protected virtual void Update() { }
        /// <inheritdoc cref="IFeature.OnLateUpdate"/>
        protected virtual void LateUpdate() { }
        /// <inheritdoc cref="IFeature.OnOnGUI"/>
        protected virtual void OnGUI() { }
        /// <inheritdoc cref="IFeature.OnEditorShownChanged"/>
        protected virtual void VisibleChanged(bool visible) { }
    }
}
