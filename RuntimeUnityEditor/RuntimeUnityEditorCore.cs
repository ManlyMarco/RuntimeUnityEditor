using System;
using System.Collections.Generic;
using System.Linq;
using BepInEx;
using RuntimeUnityEditor.Core.ObjectTree;
using RuntimeUnityEditor.Core.Preview;
using RuntimeUnityEditor.Core.Profiler;
using RuntimeUnityEditor.Core.REPL;
using RuntimeUnityEditor.Core.UI;
using RuntimeUnityEditor.Core.Utils;
using UnityEngine;

namespace RuntimeUnityEditor.Core
{
    public class RuntimeUnityEditorCore
    {
        public const string Version = "3.0";
        public const string GUID = "RuntimeUnityEditor";

        public Inspector.Inspector Inspector => Core.Inspector.Inspector.Initialized ? Core.Inspector.Inspector.Instance : null;
        public ObjectTreeViewer TreeViewer => ObjectTreeViewer.Initialized ? ObjectTreeViewer.Instance : null;
        public PreviewWindow PreviewWindow => PreviewWindow.Initialized ? PreviewWindow.Instance : null;
        public ProfilerWindow ProfilerWindow => ProfilerWindow.Initialized ? ProfilerWindow.Instance : null;
        public ReplWindow Repl => ReplWindow.Initialized ? ReplWindow.Instance : null;

        [Obsolete("No longer works", true)]
        public event EventHandler SettingsChanged;

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

        public bool ShowRepl
        {
            get => Repl != null && Repl.Enabled;
            set { if (Repl != null) Repl.Enabled = value; }
        }

        public bool EnableMouseInspect
        {
            get => MouseInspect.Initialized && MouseInspect.Instance.Enabled;
            set => MouseInspect.Instance.Enabled = value;
        }

        public bool ShowInspector
        {
            get => Inspector != null && Inspector.Enabled;
            set => Inspector.Enabled = value;
        }

        public static RuntimeUnityEditorCore Instance { get; private set; }

        internal static MonoBehaviour PluginObject => _initSettings.PluginMonoBehaviour;
        internal static ILoggerWrapper Logger => _initSettings.LoggerWrapper;
        private static InitSettings _initSettings;

        private readonly List<IFeature> _initializedFeatures = new List<IFeature>();
        private KeyCode _showHotkey = KeyCode.F12;

        //private readonly List<IWindow> _initializedWindows = new List<IWindow>();

        public RuntimeUnityEditorCore(InitSettings initSettings)
        {
            if (Instance != null)
                throw new InvalidOperationException("Can only create one instance of the Core object");

            _initSettings = initSettings;

            Instance = this;

            _onHotkeyChanged = initSettings.RegisterSetting("General", "Open/close runtime editor", KeyCode.F12, "", x => ShowHotkey = x);

            var iFeatureType = typeof(IFeature);
            // Create all instances first so they are accessible in Initialize methods in case there's crosslinking spaghetti
            var allFeatures = typeof(RuntimeUnityEditorCore).Assembly.GetTypes().Where(t => !t.IsAbstract && iFeatureType.IsAssignableFrom(t)).Select(Activator.CreateInstance).Cast<IFeature>().ToList();
            for (var index = 0; index < allFeatures.Count; index++)
            {
                var feature = allFeatures[index];
                try
                {
                    feature.OnInitialize(initSettings);
                }
                catch (Exception e)
                {
                    Logger.Log(LogLevel.Warning, $"Failed to initialize {feature.GetType().Name} - " + e);
                    continue;
                }

                _initializedFeatures.Add(feature);
                //if (feature is IWindow window)
                //    _initializedWindows.Add(window);
            }

            Logger.Log(LogLevel.Info, $"Successfully initialized {_initializedFeatures.Count}/{allFeatures.Count} features: {string.Join(", ", _initializedFeatures.Select(x => x.GetType().Name).ToArray())}");
        }

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

        public bool Show
        {
            get => TreeViewer.Enabled;
            set
            {
                // todo decouple
                if (TreeViewer.Enabled == value) return;
                TreeViewer.Enabled = value;

                for (var index = 0; index < _initializedFeatures.Count; index++)
                    _initializedFeatures[index].OnVisibleChanged(value);

                // todo safe invoke
                //ShowChanged?.Invoke(this, EventArgs.Empty);
            }
        }

        //public event EventHandler ShowChanged;

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

        public abstract class InitSettings
        {
            /// <summary>
            /// Register a new persistent setting.
            /// </summary>
            /// <typeparam name="T">Type of the setting</typeparam>
            /// <param name="category">Used for grouping</param>
            /// <param name="name">Name/Key</param>
            /// <param name="defaultValue">Initial value if setting was never changed</param>
            /// <param name="description">What the setting does</param>
            /// <param name="onValueUpdated">Called when the setting changes (except if changed by using the returned Func),
            /// and immediately when registering the setting with either the default value or the previously set value.</param>
            /// <returns>A Func that can be used to set the setting</returns>
            public abstract Action<T> RegisterSetting<T>(string category, string name, T defaultValue, string description, Action<T> onValueUpdated);
            /// <summary>
            /// Instance MB of the plugin
            /// </summary>
            public abstract MonoBehaviour PluginMonoBehaviour { get; }
            /// <summary>
            /// Log output
            /// </summary>
            public abstract ILoggerWrapper LoggerWrapper { get; }
            /// <summary>
            /// Path to write/read extra config files from
            /// </summary>
            public abstract string ConfigPath { get; }
        }
    }
}
