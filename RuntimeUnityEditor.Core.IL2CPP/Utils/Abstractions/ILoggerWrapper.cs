namespace RuntimeUnityEditor.Core.Utils.Abstractions
{
    /// <summary>
    /// A place for RUE to write logs to.
    /// </summary>
    public interface ILoggerWrapper
    {
        /// <summary>
        /// Write a log message (source is always RUE).
        /// </summary>
        /// <param name="logLevel">How important the message is.</param>
        /// <param name="content">Content of the message, can be any type so it will have to be converted to string if given log level is shown.</param>
        void Log(LogLevel logLevel, object content);
    }
}