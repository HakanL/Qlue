using System;
using System.Runtime.CompilerServices;

namespace Qlue.Logging
{
    public class Log : ILog
    {
        private ILogProvider provider;

        public Log(ILogProvider provider)
        {
            this.provider = provider;
        }

        private void SetProviderProperties()
        {
            foreach (var kvp in AsyncContext.AllKeyValues)
                this.provider.SetContextProperty(kvp.Key, kvp.Value);
        }

        private void SetExceptionProviderProperties()
        {
            // Special case to capture exception message id, etc

            var allKvp = AsyncContext.AllKeyValues;

            string messageId;
            if (allKvp.TryGetValue("MessageId", out messageId) && string.IsNullOrEmpty(messageId) && allKvp.TryGetValue("ExceptionMessageId", out messageId))
            {
                allKvp["MessageId"] = messageId;
                allKvp.Remove("ExceptionMessageId");
            }

            string customSessionId;
            if (allKvp.TryGetValue("CustomSessionId", out customSessionId) && string.IsNullOrEmpty(customSessionId) && allKvp.TryGetValue("ExceptionCustomSessionId", out customSessionId))
            {
                allKvp["CustomSessionId"] = customSessionId;
                allKvp.Remove("ExceptionCustomSessionId");
            }

            foreach (var kvp in allKvp)
                this.provider.SetContextProperty(kvp.Key, kvp.Value);
        }

        private void LogNormal(LogLevel logLevel, string message, params object[] args)
        {
            string ndc = AsyncContext.GetStackTrace(this);

            SetProviderProperties();

            this.provider.LogNormal(ndc, logLevel, message, args);
        }

        private void LogException(LogLevel logLevel, Exception exception, string message, params object[] args)
        {
            string ndc = AsyncContext.GetStackTrace(this);

            SetExceptionProviderProperties();

            this.provider.LogException(ndc, logLevel, exception, message, args);
        }

        public void Trace(string message, params object[] args)
        {
            LogNormal(LogLevel.Trace, message, args);
        }

        public void Debug(string message, params object[] args)
        {
            LogNormal(LogLevel.Debug, message, args);
        }

        public void Info(string message, params object[] args)
        {
            LogNormal(LogLevel.Info, message, args);
        }

        public void Warn(string message, params object[] args)
        {
            LogNormal(LogLevel.Warn, message, args);
        }

        public void Error(string message, params object[] args)
        {
            LogNormal(LogLevel.Error, message, args);
        }

        public void Fatal(string message, params object[] args)
        {
            LogNormal(LogLevel.Fatal, message, args);
        }

        public void ErrorException(string message, Exception exception)
        {
            LogException(LogLevel.Error, exception, message);
        }

        public void ErrorException(Exception exception, string message, params object[] args)
        {
            LogException(LogLevel.Error, exception, message, args);
        }

        public void WarnException(string message, Exception exception)
        {
            LogException(LogLevel.Warn, exception, message);
        }

        public void WarnException(Exception exception, string message, params object[] args)
        {
            LogException(LogLevel.Warn, exception, message, args);
        }

        public void Duration(string message, double durationMilliseconds)
        {
            string ndc = AsyncContext.GetStackTrace(this);

            SetProviderProperties();

            this.provider.LogDuration(ndc, LogLevel.Info, message, durationMilliseconds);
        }

        public void SetProperty(string key, string value)
        {
            AsyncContext.StoreKeyValue(key, value);
        }

        public string GetProperty(string key)
        {
            return AsyncContext.GetKeyValue(key);
        }

        public string GetProperty(string key, string defaultValue)
        {
            string value = AsyncContext.GetKeyValue(key);

            if (value == null)
                return defaultValue;

            return value;
        }

        public IDisposable Context([CallerMemberName] string contextName = "")
        {
            return new LogContext(this, contextName);
        }

        public string Name
        {
            get { return this.provider.Name; }
        }
    }
}
