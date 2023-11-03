using System;
using BepInEx.Logging;
using RuntimeUnityEditor.Core;

namespace RuntimeUnityEditor.Bepin5.LogViewer
{
    internal sealed class LogViewerListener : ILogListener
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
            if ((_owner.LogLevelFilter & eventArgs.Level) == 0)
                return;

            try
            {
                _owner.OnLogEvent(sender, eventArgs);
            }
            catch (Exception e)
            {
                RuntimeUnityEditorCore.Logger.Log(Core.Utils.Abstractions.LogLevel.Error, $"[{nameof(LogViewerWindow)}] Unexpected crash when trying to parse stack trace, disabling log capture!" + e);
                _owner.Capture = false;
            }
        }
    }
}