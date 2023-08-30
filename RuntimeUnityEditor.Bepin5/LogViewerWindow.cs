using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using BepInEx.Logging;
using RuntimeUnityEditor.Core;
using RuntimeUnityEditor.Core.Utils.Abstractions;
using UnityEngine;

namespace RuntimeUnityEditor.Bepin5
{
    // todo
    // filter by level, source and text
    // option to notify on hit?
    // inspect the source and stack trace of the log call
    public sealed class LogViewerWindow : Window<LogViewerWindow>
    {
        private bool _captureOnStartup;
        private Action<bool> _captureOnStartupCallback;
        public bool CaptureOnStartup
        {
            get => _captureOnStartup;
            set
            {
                if (_captureOnStartup != value)
                {
                    _captureOnStartup = value;
                    _captureOnStartupCallback(value);
                }
            }
        }

        private bool _capture;
        private LogViewerListener _listener;

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

        protected override void Initialize(InitSettings initSettings)
        {
            Enabled = false;
            DefaultScreenPosition = ScreenPartition.CenterLower;
            DisplayName = "Log";
            Title = "Log viewer";

            _captureOnStartupCallback = initSettings.RegisterSetting("Log Viewer", "Enable capture on startup", false, "Start capturing log messages as soon as possible after game starts.", b => _captureOnStartup = b);

            _listener = new LogViewerListener(this);
        }

        private Vector2 _scrollPosition;
        protected override void DrawContents()
        {
            GUILayout.BeginHorizontal();
            {
                if (GUILayout.Button("Clear"))
                    _logEntries.Clear();
                Capture = GUILayout.Toggle(Capture, "Capture log messages");
                CaptureOnStartup = GUILayout.Toggle(CaptureOnStartup, "Enable capture on game startup");
            }
            GUILayout.EndHorizontal();

            _scrollPosition = GUILayout.BeginScrollView(_scrollPosition, false, true);
            {
                foreach (var entry in _logEntries)
                {
                    if (GUILayout.Button(entry, GUI.skin.label))
                        GUIUtility.systemCopyBuffer = entry.text + "\n" + entry.tooltip;
                }
            }
            GUILayout.EndScrollView();
        }

        private readonly List<GUIContent> _logEntries = new List<GUIContent>();

        private void OnLogEvent(object sender, LogEventArgs eventArgs)
        {
            var stackTrace = new StackTrace(4).ToString();

            // Try to trim the stack trace up until user code
            var parts = stackTrace.Split(new char[] { '\n', '\r' }, StringSplitOptions.RemoveEmptyEntries);
            var firstGood = Array.FindIndex(parts, part => !part.Contains("at BepInEx.") && !part.Contains("at System."));
            if (firstGood >= 0)
            {
                var temp = new string[parts.Length - firstGood];
                Array.Copy(parts, firstGood, temp, 0, parts.Length - firstGood);
                stackTrace = string.Join("\n", temp);
            }

            _logEntries.Add(new GUIContent($"{DateTime.UtcNow.ToShortTimeString()} [{eventArgs.Level,-7}:{eventArgs.Source.SourceName,10}] {eventArgs.Data}", "Origin: " + stackTrace));
        }

        private sealed class LogViewerListener : ILogListener
        {
            private readonly LogViewerWindow _owner;

            public LogViewerListener(LogViewerWindow owner)
            {
                _owner = owner;
            }

            public void Dispose()
            {

            }

            public void LogEvent(object sender, LogEventArgs eventArgs)
            {
                _owner.OnLogEvent(sender, eventArgs);
            }
        }
    }
}
