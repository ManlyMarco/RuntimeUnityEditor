namespace RuntimeUnityEditor.Core.Utils.Abstractions
{
    public interface ILoggerWrapper
    {
        void Log(LogLevel logLevel, object content);
    }
}