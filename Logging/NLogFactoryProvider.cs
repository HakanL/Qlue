using System;
using System.Collections.Concurrent;
using System.Globalization;
using System.IO;
using NLog;
using NLog.LayoutRenderers;

namespace Qlue.Logging
{
    public class NLogFactoryProvider : ILogFactory
    {
        private ConcurrentDictionary<string, ILog> _logCache =
            new ConcurrentDictionary<string, ILog>();

        public void SetLogPath(string logPath)
        {
            if (logPath == null)
                throw new ArgumentNullException("logPath");

            if (!logPath.EndsWith(Path.DirectorySeparatorChar.ToString(CultureInfo.InvariantCulture), StringComparison.Ordinal))
                logPath += Path.DirectorySeparatorChar;

            NLog.GlobalDiagnosticsContext.Set("LogPath", logPath);
        }

        public NLogFactoryProvider()
        {
            // Register our mdlc layout renderer
            NLog.Config.ConfigurationItemFactory.Default.LayoutRenderers.RegisterDefinition("mdlc", typeof(MdlcLayoutRenderer));

            NLog.GlobalDiagnosticsContext.Set("TempPath", Path.GetTempPath());

            if (LogManager.Configuration != null && LogManager.Configuration.AllTargets != null)
            {
                foreach (var target in LogManager.Configuration.AllTargets)
                {
                    var layoutTarget = target as NLog.Targets.TargetWithLayout;
                    if (layoutTarget == null)
                        continue;

                    var simpleLayout = layoutTarget.Layout as NLog.Layouts.SimpleLayout;
                    if (simpleLayout == null)
                        continue;

                    if (simpleLayout.Text == "[STANDARD_QLUE_FILE_LAYOUT]")
                        layoutTarget.Layout = new NLog.Layouts.SimpleLayout("${longdate} ${pad:padding=3:inner=${event-context:item=threadid}}>${pad:padding=-5:inner=${level:uppercase=true}} ${logger}:${event-context:item=ndc} ${message}${onexception:inner=${newline}${exception:format=tostring}}");
                    else
                        if (simpleLayout.Text == "[STANDARD_QLUE_DEBUGGER_LAYOUT]")
                            layoutTarget.Layout = new NLog.Layouts.SimpleLayout("${time} ${pad:padding=3:inner=${event-context:item=threadid}}>${pad:padding=-5:inner=${level:uppercase=true}} ${logger}:${event-context:item=ndc} ${message}${onexception:inner=${newline}${exception:format=tostring}}");
                }
            }

            // Set default for LogPath
            SetLogPath(System.IO.Path.Combine(System.AppDomain.CurrentDomain.BaseDirectory, "_Logs"));
        }

        public ILog GetLogger(string name)
        {
            return _logCache.GetOrAdd(name, a => new Log(new NLogProvider(NLog.LogManager.GetLogger(name))));
        }

        public void SetGlobalProperty(string key, string value)
        {
            NLog.GlobalDiagnosticsContext.Set(key, value);
        }
    }
}
