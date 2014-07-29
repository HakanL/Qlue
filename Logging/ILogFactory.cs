using System;

namespace Qlue.Logging
{
    public interface ILogFactory
    {
        ILog GetLogger(string name);

        void SetLogPath(string logPath);

        void SetGlobalProperty(string key, string value);
    }
}
