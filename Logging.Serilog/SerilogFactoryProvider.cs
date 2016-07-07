using System;
using System.Collections.Concurrent;
using System.Globalization;
using System.IO;
using Serilog;

namespace Qlue.Logging
{
    public class SerilogFactoryProvider : ILogFactory
    {
        private ConcurrentDictionary<string, ILog> _logCache =
            new ConcurrentDictionary<string, ILog>();

        private LoggerConfiguration _loggerConfig;
        private Serilog.Core.Logger _logger;

        public SerilogFactoryProvider(LoggerConfiguration loggerConfig)
        {
            _loggerConfig = loggerConfig.Enrich.FromLogContext();
        }

        public void SetLogPath(string logPath)
        {
            if (logPath == null)
                throw new ArgumentNullException("logPath");

            if (!logPath.EndsWith(Path.DirectorySeparatorChar.ToString(CultureInfo.InvariantCulture), StringComparison.Ordinal))
                logPath += Path.DirectorySeparatorChar;

            _loggerConfig = _loggerConfig.WriteTo.File(logPath);
        }

        private Serilog.Core.Logger Logger
        {
            get
            {
                lock (this)
                {
                    if (_logger == null)
                        _logger = _loggerConfig.CreateLogger();

                    return _logger;
                }
            }
        }

        public ILog GetLogger(string name)
        {
            return _logCache.GetOrAdd(name,
                a => new Log(new SerilogProvider(Logger, name)));
        }

        public void SetGlobalProperty(string key, string value)
        {
        }
    }
}
