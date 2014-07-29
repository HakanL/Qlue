using System;

namespace Qlue.Logging
{
    public class CustomLogProvider : ILogProvider
    {
        private string name;
        private Action<string, string, LogLevel, Exception, string> logAction;

        internal CustomLogProvider(
            string name,
            Action<string, string, LogLevel, Exception, string> logAction)
        {
            this.name = name;
            this.logAction = logAction;
        }

        public void LogNormal(string ndc, LogLevel logLevel, string message, params object[] args)
        {
            this.logAction(Name, ndc, logLevel, null, string.Format(message, args));
        }

        public void LogException(string ndc, LogLevel logLevel, Exception exception, string message, params object[] args)
        {
            this.logAction(Name, ndc, logLevel, exception, string.Format(message, args));
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
            get { return this.name; }
        }
    }
}
