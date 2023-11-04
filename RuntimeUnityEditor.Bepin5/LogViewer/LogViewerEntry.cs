using System;
using System.Diagnostics;
using System.Linq;
using System.Reflection;
using System.Text;
using BepInEx.Logging;
using RuntimeUnityEditor.Core;
using RuntimeUnityEditor.Core.Utils;
using UnityEngine;

namespace RuntimeUnityEditor.Bepin5.LogViewer
{
    internal readonly struct LogViewerEntry
    {
        private LogViewerEntry(object sender, LogEventArgs logEventArgs, StackFrame[] filteredStackTrace, string filteredStackTraceString)
        {
            Sender = sender;
            LogEventArgs = logEventArgs;
            FilteredStackTrace = filteredStackTrace;
            FilteredStackTraceString = filteredStackTraceString;

            Method = filteredStackTrace != null && filteredStackTrace.Length > 0 ? filteredStackTrace[0].GetMethod() : null;

            var tooltip = "Filtered stack trace of this log write:\n\n" + filteredStackTraceString;
            _timeString = new GUIContent(DateTime.UtcNow.ToShortTimeString(), tooltip);
            _logLevelString = new GUIContent(logEventArgs.Level.ToString(), tooltip);
            _sourceNameString = new GUIContent(logEventArgs.Source.SourceName, tooltip);
            _dataString = new GUIContent(logEventArgs.Data?.ToString() ?? "NULL", tooltip);
            //ContentLevel = new GUIContent($"{_TimeString} [{_logLevelString,-7}:{_sourceSourceNameString,10}]", tooltip);
            //ContentText = new GUIContent(_dataString, tooltip);
        }

        private readonly GUIContent _timeString;
        private readonly GUIContent _logLevelString;
        private readonly GUIContent _sourceNameString;
        private readonly GUIContent _dataString;

        public MethodBase Method { get; }
        //public GUIContent ContentLevel { get; }
        //public GUIContent ContentText { get; }
        public LogEventArgs LogEventArgs { get; }
        public StackFrame[] FilteredStackTrace { get; }
        public string FilteredStackTraceString { get; }
        public object Sender { get; }

        public string GetClipboardString()
        {
            return $"{_timeString} {_logLevelString} {_sourceNameString} {_dataString}\n{FilteredStackTraceString}\nSender: {Sender} ({Sender?.GetType().GetSourceCodeRepresentation() ?? "NULL"})";
        }

        public bool DrawEntry()
        {
            GUI.color = GetColor();
            var clicked = GUILayout.Button(_timeString, GUI.skin.label, GUILayout.MinWidth(35));
            GUILayout.Label("[", GUILayout.ExpandWidth(false));
            clicked |= GUILayout.Button(_logLevelString, GUI.skin.label, GUILayout.MinWidth(45));
            GUILayout.Label(":", GUILayout.ExpandWidth(false));
            clicked |= GUILayout.Button(_sourceNameString, GUI.skin.label, GUILayout.MinWidth(100));
            GUILayout.Label("]", GUILayout.ExpandWidth(false));
            GUI.color = Color.white;
            clicked |= GUILayout.Button(_dataString, GUI.skin.label, GUILayout.ExpandWidth(true));
            return clicked;
        }

        public Color GetColor()
        {
            switch (LogEventArgs.Level)
            {
                case LogLevel.Fatal:
                case LogLevel.Error:
                    return Color.red;

                case LogLevel.Warning:
                    return Color.yellow;

                default:
                case LogLevel.Message:
                case LogLevel.Info:
                    return Color.white;

                case LogLevel.Debug:
                    return Color.gray;
            }
        }

        public bool IsMatch(string searchString, LogLevel logLevelFilter)
        {
            return (logLevelFilter & LogEventArgs.Level) != 0
                   && (string.IsNullOrEmpty(searchString)
                       || _dataString.text.Contains(searchString, StringComparison.OrdinalIgnoreCase)
                       || FilteredStackTraceString.Contains(searchString, StringComparison.OrdinalIgnoreCase)
                       || LogEventArgs.Source.SourceName.Contains(searchString, StringComparison.OrdinalIgnoreCase));
        }

        #region Parsing

        private const int SkippedStackFrames = 4;
        private static bool _stacktraceTostringFallback;

        public static LogViewerEntry CreateFromLogEventArgs(object sender, LogEventArgs eventArgs)
        {
            string hoverText;
            var st = new StackTrace(SkippedStackFrames);
            StackFrame[] frames = null;

        fallback:
            if (!_stacktraceTostringFallback)
            {
                try
                {
                    // Try to trim the stack trace up until user code and reduce the amount of text shown so it fits in the tooltip
                    hoverText = ParseStackTrace(st, out frames);
                }
                catch (Exception e)
                {
                    RuntimeUnityEditorCore.Logger.Log(Core.Utils.Abstractions.LogLevel.Error, $"[{nameof(LogViewerWindow)}] Crash when trying to parse stack trace, falling back to using ToString\n" + e);
                    _stacktraceTostringFallback = true;
                    goto fallback;
                }
            }
            else
            {
                hoverText = ParseStackTraceString(st);
                try
                {
                    frames = st.GetFrames();
                }
                catch (Exception e)
                {
                    Console.WriteLine(e);
                }
            }

            return new LogViewerEntry(sender, eventArgs, frames, hoverText);
        }
        
        private static string ParseStackTrace(StackTrace st, out StackFrame[] filteredFrames)
        {
            filteredFrames = null;

            var frames = st.GetFrames();
            if (frames == null || frames.Length == 0)
                return ParseStackTraceString(st);

            var sb = new StringBuilder();
            var realEncountered = false;
            var firstRealIdx = 0;
            //var skipped = 0;
            for (var i = 0; i < frames.Length; i++)
            {
                var frame = frames[i];
                var m = frame.GetMethod();
                var mName = m.Name;
                var typeName = (m.DeclaringType ?? m.ReflectedType)?.GetSourceCodeRepresentation() ?? "???";

                var first = false;
                if (!realEncountered)
                {
                    if (typeName.StartsWith("BepInEx.", StringComparison.Ordinal) ||
                        typeName.StartsWith("System.", StringComparison.Ordinal) ||
                        typeName.StartsWith("UnityEngine.", StringComparison.Ordinal) && mName.Contains("Log", StringComparison.Ordinal) ||
                        typeName.StartsWith(nameof(RuntimeUnityEditor), StringComparison.Ordinal) && mName.Equals("Log", StringComparison.Ordinal))
                    {
                        //skipped++;
                        continue;
                    }

                    realEncountered = true;
                    first = true;
                }

                if (first)
                    firstRealIdx = i;

                sb.AppendFormat("[{0}] ", i + SkippedStackFrames);
                sb.Append(typeName);
                sb.Append('.');
                sb.Append(mName);
                //todo params
                sb.AppendLine();
            }

            //if (skipped > 0)
            //    sb.AppendLine($"(The stack trace starts at frame {4 + skipped})");

            filteredFrames = frames.Skip(firstRealIdx).ToArray();

            if (sb.Length == 0)
                sb.Append(@"¯\_(ツ)_/¯");

            return sb.ToString();
        }

        private static string ParseStackTraceString(StackTrace st)
        {
            var parts = st.ToString().Split(new[] { '\n', '\r' }, StringSplitOptions.RemoveEmptyEntries);
            var firstGood = Array.FindIndex(parts, part => !part.Contains("at BepInEx.") && !part.Contains("at System."));
            if (firstGood >= 0)
            {
                var temp = new string[parts.Length - firstGood];
                Array.Copy(parts, firstGood, temp, 0, parts.Length - firstGood);
                return "Origin: " + string.Join("\n", temp);
            }

            return string.Empty;
        }

        #endregion
    }
}