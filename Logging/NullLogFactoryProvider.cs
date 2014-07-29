using System;

namespace Qlue.Logging
{
    public class NullLogFactoryProvider : ILogFactory
    {
        public ILog GetLogger(string name)
        {
            return new Log(new NullLogProvider());
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
