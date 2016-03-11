using System;
using Serilog.Core;
using Serilog.Core.Enrichers;
using Serilog.Enrichers;

namespace Qlue.Logging
{
    public class SerilogProvider : ILogProvider
    {
        private Serilog.ILogger logger;
        private string name;

        internal SerilogProvider(Serilog.ILogger logger, string name)
        {
            this.logger = logger;
            this.name = name;
        }

        private static Serilog.Events.LogEventLevel GetLogLevel(Qlue.Logging.LogLevel logLevel)
        {
            switch (logLevel)
            {
                case LogLevel.Trace:
                    return Serilog.Events.LogEventLevel.Verbose;

                case LogLevel.Debug:
                    return Serilog.Events.LogEventLevel.Debug;

                case LogLevel.Info:
                    return Serilog.Events.LogEventLevel.Information;

                case LogLevel.Warn:
                    return Serilog.Events.LogEventLevel.Warning;

                case LogLevel.Error:
                    return Serilog.Events.LogEventLevel.Error;

                case LogLevel.Fatal:
                    return Serilog.Events.LogEventLevel.Fatal;

                default:
                    throw new ArgumentException("Unknown LogLevel");
            }
        }
        
        public void SetContextProperty(string key, string value)
        {
            Qlue.Logging.MappedDiagnosticsLogicalContext.Set(key, value);
        }

        public void LogNormal(string ndc, LogLevel logLevel, string message, params object[] args)
        {
            var properties = new ILogEventEnricher[]
            {
                new ThreadIdEnricher(),
                new MachineNameEnricher(),
                new PropertyEnricher("NDC", ndc),
                new PropertyEnricher("Logger", this.Name)
            };

            using (Serilog.Context.LogContext.PushProperties(properties))
            {
                this.logger.Write(GetLogLevel(logLevel), message, args);
            }
        }

        public void LogException(string ndc, LogLevel logLevel, Exception exception, string message, params object[] args)
        {
            var properties = new ILogEventEnricher[]
            {
                new ThreadIdEnricher(),
                new MachineNameEnricher(),
                new PropertyEnricher("NDC", ndc),
                new PropertyEnricher("Logger", this.Name),
                new PropertyEnricher("ExceptionType", exception.GetType().Name)
            };

            using (Serilog.Context.LogContext.PushProperties(properties))
            {
                this.logger.Write(GetLogLevel(logLevel), exception, message, args);
            }
        }

        public void LogDuration(string ndc, LogLevel logLevel, string message, double durationMilliseconds)
        {
            var properties = new ILogEventEnricher[]
            {
                new ThreadIdEnricher(),
                new MachineNameEnricher(),
                new PropertyEnricher("NDC", ndc),
                new PropertyEnricher("Logger", this.Name),
                new PropertyEnricher("DurationMS", Math.Round(durationMilliseconds,1))
            };

            using (Serilog.Context.LogContext.PushProperties(properties))
            {
                this.logger.Write(GetLogLevel(logLevel), message, null);
            }
        }

        public string Name
        {
            get { return this.name; }
        }
    }
}
