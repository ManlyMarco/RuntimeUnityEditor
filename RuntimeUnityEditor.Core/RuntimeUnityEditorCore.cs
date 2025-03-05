using System;
using System.Collections.Generic;
using System.Linq;
using RuntimeUnityEditor.Core.ObjectTree;
using RuntimeUnityEditor.Core.ObjectView;
using RuntimeUnityEditor.Core.Profiler;
using RuntimeUnityEditor.Core.REPL;
using RuntimeUnityEditor.Core.UI;
using RuntimeUnityEditor.Core.Utils;
using RuntimeUnityEditor.Core.Utils.Abstractions;
using UnityEngine;
#pragma warning disable CS0618

namespace RuntimeUnityEditor.Core
{
    /// <summary>
    /// Main class of RUE. It initializes and manages all of the features, and propagates events to them.
    /// To access individual features, reference them directly by using <see cref="FeatureBase{T}.Initialized"/> and <see cref="FeatureBase{T}.Instance"/>.
    /// </summary>
    public class RuntimeUnityEditorCore
    {
        /// <summary>
        /// Version constant for use in version checks.
        /// Beware that this is a const and it will be burned as a string into your assembly at build time.
        /// To see the version that is currently installed use <see cref="InstalledVersion"/>.
        /// </summary>
        public const string Version = "5.6"; // REMEMBER TO UPDATE Version IN IL2CPP CSPROJ TOO! TODO: Automate this

        /// <summary>
        /// Get the currently installed version at runtime.
        /// For use whenever the running instance version number is needed, instead of the version of the RUE assembly your plugin was compiled against.
        /// </summary>
        public static string InstalledVersion => Version;

        /// <summary>
        /// GUID for use in version and dependency checks.
        /// Beware that this is a const and it will be burned as a string into your assembly. This shouldn't be an issue since this should never change.
        /// </summary>
        public const string GUID = "RuntimeUnityEditor";

        #region Obsolete

        /// <summary> Obsolete, do not use. Will be removed soon. </summary>
        [Obsolete("Use window Instance instead", true)] public Inspector.Inspector Inspector => Core.Inspector.Inspector.Initialized ? Core.Inspector.Inspector.Instance : null;
        /// <summary> Obsolete, do not use. Will be removed soon. </summary>
        [Obsolete("Use window Instance instead", true)] public ObjectTreeViewer TreeViewer => ObjectTreeViewer.Initialized ? ObjectTreeViewer.Instance : null;
        /// <summary> Obsolete, do not use. Will be removed soon. </summary>
        [Obsolete("Use window Instance instead", true)] public ObjectViewWindow PreviewWindow => ObjectViewWindow.Initialized ? ObjectViewWindow.Instance : null;
        /// <summary> Obsolete, do not use. Will be removed soon. </summary>
        [Obsolete("Use window Instance instead", true)] public ProfilerWindow ProfilerWindow => ProfilerWindow.Initialized ? ProfilerWindow.Instance : null;
        /// <summary> Obsolete, do not use. Will be removed soon. </summary>
        [Obsolete("Use window Instance instead", true)] public ReplWindow Repl => ReplWindow.Initialized ? ReplWindow.Instance : null;
        /// <summary> Obsolete, do not use. Will be removed soon. </summary>
        [Obsolete("No longer works", true)] public event EventHandler SettingsChanged;

        /// <summary>
        /// Hotkey used to show/hide RuntimeUnityEditor. Changing this at runtime also updates the config file, so the value will be set on the next start.
        /// </summary>
        [Obsolete("Avoid changing the hotkey through code since it will overwrite user setting. Set the Show property instead if you need to show/hide RUE at specific times.")]
        public KeyCode ShowHotkey
        {
            get => _showHotkey;
            set
            {
                if (_showHotkey != value)
                {
                    _showHotkey = value;
                    _onHotkeyChanged?.Invoke(value);
                }
            }
        }

        private readonly Action<KeyCode> _onHotkeyChanged;

        /// <summary> Obsolete, do not use. Will be removed soon. </summary>
        [Obsolete("Use window Instance instead", true)]
        public bool ShowRepl
        {
            get => ReplWindow.Initialized && ReplWindow.Instance.Enabled;
            set { if (ReplWindow.Initialized) ReplWindow.Instance.Enabled = value; }
        }

        /// <summary> Obsolete, do not use. Will be removed soon. </summary>
        [Obsolete("Use window Instance instead", true)]
        public bool EnableMouseInspect
        {
            get => MouseInspect.Initialized && MouseInspect.Instance.Enabled;
            set { if (MouseInspect.Initialized) MouseInspect.Instance.Enabled = value; }
        }

        /// <summary> Obsolete, do not use. Will be removed soon. </summary>
        [Obsolete("Use window Instance instead", true)]
        public bool ShowInspector
        {
            get => Core.Inspector.Inspector.Initialized && Core.Inspector.Inspector.Instance.Enabled;
            set { if (Core.Inspector.Inspector.Initialized) Core.Inspector.Inspector.Instance.Enabled = value; }
        }

        #endregion

        #region Public API

        /// <summary>
        /// Current instance of RuntimeUnityEditor.
        /// Use <see cref="IsInitialized"/> to check if initialization has been finished.
        /// </summary>
        public static RuntimeUnityEditorCore Instance { get; private set; }

        /// <summary>
        /// Check if RuntimeUnityEditor has finished initializing.
        /// If this method is called from a background thread and the initialization is currently
        /// in progress, it will block until initialization finishes (or fails).
        /// </summary>
        public static bool IsInitialized()
        {
            lock (GUID)
            {
                return Instance != null;
            }
        }

        /// <summary>
        /// Show RuntimeUnityEditor interface (global toggle controlled by the hotkey).
        /// When hidden, most of the features are disabled/paused to minimize performance penalty.
        /// </summary>
        public bool Show
        {
            get => Taskbar.Instance.Enabled;
            set
            {
                if (Taskbar.Instance.Enabled == value) return;
                Taskbar.Instance.Enabled = value;

                for (var index = 0; index < _initializedFeatures.Count; index++)
                    _initializedFeatures[index].OnEditorShownChanged(value);
            }
        }

        /// <summary>
        /// Features that have been successfully initialized so far and are available to the user.
        /// </summary>
        public IEnumerable<IFeature> InitializedFeatures => _initializedFeatures;

        /// <summary>
        /// Add a new feature to RuntimeUnityEditor.
        /// Will throw if the feature fails to initialize.
        /// </summary>
        public void AddFeature(IFeature feature)
        {
            AddFeatureInt(feature);
            Taskbar.Instance.SetFeatures(_initializedFeatures);
        }

        internal bool RemoveFeature(IFeature feature)
        {
            if (_initializedFeatures.Remove(feature))
            {
                Taskbar.Instance.SetFeatures(_initializedFeatures);
                return true;
            }
            return false;
        }

        #endregion

        internal static MonoBehaviour PluginObject => _initSettings.PluginMonoBehaviour;
        internal static ILoggerWrapper Logger => _initSettings.LoggerWrapper;

        private static InitSettings _initSettings;
        private readonly List<IFeature> _initializedFeatures = new List<IFeature>();
        private KeyCode _showHotkey = KeyCode.F12;

        /// <summary>
        /// Initialize RuntimeUnityEditor. Can only be ran once. Must run on the main Unity thread.
        /// Must complete before accessing any of RuntimeUnityEditor's features or they may not be initialized.
        /// </summary>
        internal RuntimeUnityEditorCore(InitSettings initSettings)
        {
            lock (GUID)
            {
                if (Instance != null)
                    throw new InvalidOperationException("Can create only one instance of the Core object");

                _initSettings = initSettings;

                Instance = this;

                try
                {
                    _onHotkeyChanged = initSettings.RegisterSetting("General", "Open/close runtime editor", KeyCode.F12, "", x => ShowHotkey = x);

                    var iFeatureType = typeof(IFeature);
                    // Create all instances first so they are accessible in Initialize methods in case there's crosslinking spaghetti
                    var allFeatures = typeof(RuntimeUnityEditorCore).Assembly.GetTypesSafe()
                                                                    .Where(t => !t.IsAbstract && iFeatureType.IsAssignableFrom(t))
                                                                    .Select(Activator.CreateInstance)
                                                                    .Cast<IFeature>().ToList();

                    foreach (var feature in allFeatures)
                    {
                        try
                        {
                            AddFeatureInt(feature);
                        }
                        catch (Exception e)
                        {
                            if (feature is Taskbar)
                                throw new InvalidOperationException("WindowManager somehow failed to initialize! I am die, thank you forever.", e);

                            Logger.Log(LogLevel.Warning, $"Failed to initialize {feature.GetType().Name} - {(e is NotSupportedException ? e.Message : e.ToString())}");
                        }
                    }

                    Taskbar.Instance.SetFeatures(_initializedFeatures);

                    Logger.Log(LogLevel.Info, $"Successfully initialized {_initializedFeatures.Count}/{allFeatures.Count} features: {string.Join(", ", _initializedFeatures.Select(x => x.GetType().Name).ToArray())}");
                }
                catch
                {
                    Instance = null;
                    throw;
                }
            }
        }

        private void AddFeatureInt(IFeature feature)
        {
            feature.OnInitialize(_initSettings);

            _initializedFeatures.Add(feature);
        }

        #region Unity callbacks (called by the modloader-specific part of this plugin)

        internal void OnGUI()
        {
            if (Show)
            {
                var originalSkin = GUI.skin;
                GUI.skin = InterfaceMaker.CustomSkin;

                for (var index = 0; index < _initializedFeatures.Count; index++)
                    _initializedFeatures[index].OnOnGUI();

                // Restore old skin for maximum compatibility
                GUI.skin = originalSkin;
            }
        }

        internal void Update()
        {
            if (UnityInput.Current.GetKeyDown(ShowHotkey))
                Show = !Show;

            if (Show)
            {
                for (var index = 0; index < _initializedFeatures.Count; index++)
                    _initializedFeatures[index].OnUpdate();
            }
        }

        internal void LateUpdate()
        {
            if (Show)
            {
                for (var index = 0; index < _initializedFeatures.Count; index++)
                    _initializedFeatures[index].OnLateUpdate();
            }
        }

        #endregion
    }
}
