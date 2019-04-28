namespace RuntimeUnityEditor.Core
{
    public interface ILoggerWrapper
    {
        void Log(LogLevel logLogLevel, object content);
    }
}