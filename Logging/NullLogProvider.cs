using System;

namespace Qlue.Logging
{
    public class NullLogProvider : ILogProvider
    {
        internal NullLogProvider()
        {
        }

        public void LogNormal(string ndc, LogLevel logLevel, string message, params object[] args)
        {
            // NOP
        }

        public void LogException(string ndc, LogLevel logLevel, Exception exception, string message, params object[] args)
        {
            // NOP
        }

        public void LogDuration(string ndc, LogLevel logLevel, string message, double durationMilliseconds)
        {
            // NOP
        }

        public void SetContextProperty(string key, string value)
        {
            // NOP
        }

        public string Name
        {
            get { return string.Empty; }
        }
    }
}
