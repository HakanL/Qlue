using System;

namespace Qlue.Logging
{
    public class CustomLogFactoryProvider : ILogFactory
    {
        private Action<string, string, LogLevel, Exception, string> logAction;

        public CustomLogFactoryProvider(Action<string, string, LogLevel, Exception, string> logAction)
        {
            this.logAction = logAction;
        }

        public ILog GetLogger(string name)
        {
            return new Log(new CustomLogProvider(name, logAction));
        }

        public void SetLogPath(string logPath)
        {
            // NOP
        }

        public void SetGlobalProperty(string key, string value)
        {
            // NOP
        }
    }
}
