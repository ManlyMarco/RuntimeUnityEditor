using BepInEx.Logging;
using RuntimeUnityEditor.Core;
using RuntimeUnityEditor.Core.Inspector;
using RuntimeUnityEditor.Core.Inspector.Entries;
using RuntimeUnityEditor.Core.Utils;
using RuntimeUnityEditor.Core.Utils.Abstractions;
using UnityEngine;
using ContextMenu = RuntimeUnityEditor.Core.ContextMenu;
using LogLevel = BepInEx.Logging.LogLevel;

namespace RuntimeUnityEditor.Bepin6.LogViewer
{
    /// <summary>
    /// A way to display and filter BepInEx log messages.
    /// </summary>
    public sealed class LogViewerWindow : Window<LogViewerWindow>
    {
        private InitSettings.Setting<bool> _captureOnStartup;
        /// <summary>
        /// Enable on game startup
        /// </summary>
        public bool CaptureOnStartup
        {
            get => _captureOnStartup.Value;
            set => _captureOnStartup.Value = value;
        }

        private bool _capture;
        private LogViewerListener _listener;

        /// <summary>
        /// Enable capturing log messages
        /// </summary>
        public bool Capture
        {
            get => _capture;
            set
            {
                if (_capture != value)
                {
                    _capture = value;

                    if (_capture)
                        BepInEx.Logging.Logger.Listeners.Add(_listener);
                    else
                        BepInEx.Logging.Logger.Listeners.Remove(_listener);
                }
            }
        }

        private InitSettings.Setting<LogLevel> _logLevelFilter;
        /// <summary>
        /// Which log levels to capture
        /// </summary>
        public LogLevel LogLevelFilter
        {
            get => _logLevelFilter.Value;
            set => _logLevelFilter.Value = value;
        }

        private string _searchString;
        /// <summary>
        /// Filter log messages by this string
        /// </summary>
        public string SearchString
        {
            get => _searchString;
            set
            {
                if (_searchString != value)
                {
                    _searchString = value;

                    UpdateFilteredLogEntries();
                }
            }
        }

        private void UpdateFilteredLogEntries()
        {
            _filteredLogEntries.Clear();
            _filteredLogEntries.AddRange(_logEntries.Where(entry => entry.IsMatch(SearchString, LogLevelFilter)));
        }

        private readonly List<LogViewerEntry> _logEntries = new List<LogViewerEntry>();
        private readonly List<LogViewerEntry> _filteredLogEntries = new List<LogViewerEntry>();

        /// <inheritdoc />
        protected override void Initialize(InitSettings initSettings)
        {
            Enabled = false;
            DefaultScreenPosition = ScreenPartition.CenterLower;
            DisplayName = "Logger";
            Title = "BepInEx Log Viewer";
            MinimumSize = new Vector2(640, 200);

            _listener = new LogViewerListener(this);

            _captureOnStartup = initSettings.RegisterSetting("Log Viewer", "Enable capture on startup", false, "Start capturing log messages as soon as possible after game starts.");
            if (_captureOnStartup.Value)
                Capture = true;

            _logLevelFilter = initSettings.RegisterSetting("Log Viewer", "Log level filter", LogLevel.All, "Filter captured log messages by their log level.");
            _logLevelFilter.ValueChanged += _ => UpdateFilteredLogEntries();

            string GetTrimmedTypeName(ILogSource obj)
            {
                var fullName = obj?.GetType().GetSourceCodeRepresentation() ?? "NULL";
                if (fullName.StartsWith("BepInEx.Logging.", StringComparison.Ordinal))
                    fullName = fullName.Substring("BepInEx.Logging.".Length);
                return fullName;
            }
            ToStringConverter.AddConverter<ILogSource>(obj => $"{obj.SourceName} ({GetTrimmedTypeName(obj)})");
            //ToStringConverter.AddConverter<ILogListener>(obj => $"{obj.GetType().FullName}");
        }

        private Vector2 _scrollPosition;
        // TODO private int _itemHeight = 30; needs a better way to measure, might have to hold size of each log entry and recalculate on window resize

        /// <inheritdoc />
        protected override void DrawContents()
        {
            GUILayout.BeginHorizontal();
            {
                GUILayout.BeginHorizontal(GUI.skin.box, IMGUIUtils.LayoutOptionsExpandWidthTrue);
                {
                    GUI.changed = false;
                    var searchString = SearchString;
                    var isEmpty = string.IsNullOrEmpty(searchString) && GUI.GetNameOfFocusedControl() != "sbox";
                    if (isEmpty) GUI.color = Color.gray;
                    GUI.SetNextControlName("sbox");
                    var newString = GUILayout.TextField(isEmpty ? "Search log text and stack traces..." : searchString, IMGUIUtils.LayoutOptionsExpandWidthTrue);
                    if (GUI.changed) SearchString = newString;
                    GUI.color = Color.white;
                }
                GUILayout.EndHorizontal();
                GUILayout.BeginHorizontal(GUI.skin.box, IMGUIUtils.LayoutOptionsExpandWidthFalse);
                {
                    if (!Capture) GUI.color = Color.red;
                    Capture = GUILayout.Toggle(Capture, new GUIContent("Enable log capture", null, "Note: This can hurt performance, especially if there is log spam."));
                    GUI.color = Color.white;
                    CaptureOnStartup = GUILayout.Toggle(CaptureOnStartup, new GUIContent("Enable on game startup", null, "Warning: This can hurt performance, especially after running for a while!"));

                    if (GUILayout.Button("Clear the list"))
                    {
                        _logEntries.Clear();
                        _filteredLogEntries.Clear();
                    }
                }
                GUILayout.EndHorizontal();
            }
            GUILayout.EndHorizontal();

            GUILayout.BeginHorizontal();
            {
                GUILayout.BeginHorizontal(GUI.skin.box);
                {
                    GUILayout.Label(new GUIContent("Captured log levels:", null, "Only new log messages with these levels will be captured, therefore enabling levels will not show past log messages!"));
                    var filter = LogLevelFilter;
                    foreach (var logLevel in new[] { LogLevel.Debug, LogLevel.Info, LogLevel.Message, LogLevel.Warning, LogLevel.Error, LogLevel.Fatal, LogLevel.All })
                    {
                        GUI.changed = false;
                        var result = GUILayout.Toggle((filter & logLevel) != 0, logLevel.ToString());
                        if (GUI.changed)
                            LogLevelFilter = result ? filter | logLevel : filter & ~logLevel;
                    }
                }
                GUILayout.EndHorizontal();

                GUILayout.FlexibleSpace();

                GUILayout.BeginHorizontal(GUI.skin.box);
                {
                    if (GUILayout.Button("Sources")) Inspector.Instance.Push(new InstanceStackEntry(BepInEx.Logging.Logger.Sources, "Log Sources"), true);
                    if (GUILayout.Button("Listeners")) Inspector.Instance.Push(new InstanceStackEntry(BepInEx.Logging.Logger.Listeners, "Log Listeners"), true);
                }
                GUILayout.EndHorizontal();
            }
            GUILayout.EndHorizontal();

            _scrollPosition = GUILayout.BeginScrollView(_scrollPosition, false, true);
            {
                var logEntries = _filteredLogEntries;
                foreach(var entry in logEntries)
                {
                    GUILayout.BeginHorizontal(GUI.skin.box);
                    {
                        if (entry.DrawEntry())
                        {
                            if (IMGUIUtils.IsMouseRightClick() && ContextMenu.Initialized)
                            {
                                // todo better right click menu
                                if (entry.Sender != null)
                                    ContextMenu.Instance.Show(entry.Sender);
                                else
                                    RuntimeUnityEditorCore.Logger.Log(Core.Utils.Abstractions.LogLevel.Warning, $"[{nameof(LogViewerWindow)}] Sender is null, cannot inspect");
                            }
                            else
                            {
                                try
                                {
                                    UnityFeatureHelper.systemCopyBuffer = entry.GetClipboardString();
                                    RuntimeUnityEditorCore.Logger.Log(Core.Utils.Abstractions.LogLevel.Message, $"[{nameof(LogViewerWindow)}] Copied to clipboard");
                                }
                                catch (Exception e)
                                {
                                    RuntimeUnityEditorCore.Logger.Log(Core.Utils.Abstractions.LogLevel.Message | Core.Utils.Abstractions.LogLevel.Error, $"[{nameof(LogViewerWindow)}] Failed to copy to clipboard: " + e.Message);
                                }
                            }
                        }

                        if (entry.Sender != null && GUILayout.Button("Inspect", IMGUIUtils.LayoutOptionsExpandWidthFalse))
                            Inspector.Instance.Push(new InstanceStackEntry(entry, entry.LogEventArgs.Source.SourceName + " -> Log entry"), true);

                        DnSpyHelper.DrawDnSpyButtonIfAvailable(entry.Method, new GUIContent("^", null, $"In dnSpy, attempt to navigate to the method that produced this log message:\n\n{entry.Method.GetFancyDescription()}"));
                    }
                    GUILayout.EndHorizontal();
                }
            }
            GUILayout.EndScrollView();

            TooltipWidth = Mathf.Min(777, Mathf.Max(300, (int)WindowRect.width - 100));
        }

        internal void OnLogEvent(object sender, LogEventArgs eventArgs)
        {
            try
            {
                var entry = LogViewerEntry.CreateFromLogEventArgs(sender, eventArgs);
                AddEntry(entry);
            }
            catch (Exception e)
            {
                Console.WriteLine(e);
            }
        }

        private void AddEntry(LogViewerEntry entry)
        {
            _logEntries.Add(entry);
            if (entry.IsMatch(SearchString, LogLevelFilter))
                _filteredLogEntries.Add(entry);
        }
    }
}
