using System;
using System.Globalization;
using NLog;

namespace Qlue.Logging
{
    public class NLogProvider : ILogProvider
    {
        private NLog.Logger logger;

        internal NLogProvider(NLog.Logger logger)
        {
            this.logger = logger;
        }

        private static NLog.LogLevel GetLogLevel(Qlue.Logging.LogLevel logLevel)
        {
            switch (logLevel)
            {
                case LogLevel.Trace:
                    return NLog.LogLevel.Trace;

                case LogLevel.Debug:
                    return NLog.LogLevel.Debug;

                case LogLevel.Info:
                    return NLog.LogLevel.Info;

                case LogLevel.Warn:
                    return NLog.LogLevel.Warn;

                case LogLevel.Error:
                    return NLog.LogLevel.Error;

                case LogLevel.Fatal:
                    return NLog.LogLevel.Fatal;

                default:
                    throw new ArgumentException("Unknown LogLevel");
            }
        }

        private LogEventInfo CreateLogEventInfo(string ndc, LogLevel logLevel, string message)
        {
            var logEvent = new LogEventInfo(
                GetLogLevel(logLevel),
                this.logger.Name,
                message);

            logEvent.Properties.Add("ndc", ndc);
            logEvent.Properties.Add("threadid", System.Threading.Thread.CurrentThread.ManagedThreadId.ToString(CultureInfo.InvariantCulture));

            // Copy properties
            foreach (var kvp in Qlue.Logging.MappedDiagnosticsLogicalContext.All)
                logEvent.Properties.Add(kvp.Key, kvp.Value);

            return logEvent;
        }

        public void SetContextProperty(string key, string value)
        {
            Qlue.Logging.MappedDiagnosticsLogicalContext.Set(key, value);
        }

        public void LogNormal(string ndc, LogLevel logLevel, string message, params object[] args)
        {
            var logEvent = CreateLogEventInfo(ndc, logLevel, message);

            logEvent.Parameters = args;

            this.logger.Log(logEvent);
        }

        public void LogException(string ndc, LogLevel logLevel, Exception exception, string message, params object[] args)
        {
            var logEvent = CreateLogEventInfo(ndc, logLevel, message);

            logEvent.Parameters = args;
            logEvent.Exception = exception;

            this.logger.Log(logEvent);
        }

        public void LogDuration(string ndc, LogLevel logLevel, string message, double durationMilliseconds)
        {
            var logEvent = CreateLogEventInfo(ndc, logLevel, message);

            logEvent.Properties["DurationMS"] = durationMilliseconds.ToString("F1", CultureInfo.InvariantCulture);

            this.logger.Log(logEvent);
        }

        public string Name
        {
            get { return this.logger.Name; }
        }
    }
}
