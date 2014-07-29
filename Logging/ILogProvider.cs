using System;

namespace Qlue.Logging
{
    public interface ILogProvider
    {
        void LogNormal(string ndc, LogLevel logLevel, string message, params object[] args);

        void LogException(string ndc, LogLevel logLevel, Exception exception, string message, params object[] args);

        void LogDuration(string ndc, LogLevel logLevel, string message, double durationMilliseconds);

        void SetContextProperty(string key, string value);

        string Name { get; }
    }
}
