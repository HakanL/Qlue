using System;
using System.Runtime.CompilerServices;

namespace Qlue.Logging
{
    public interface ILog
    {
        void Trace(string message, params object[] args);

        void Debug(string message, params object[] args);

        void Info(string message, params object[] args);

        void Warn(string message, params object[] args);

        void Error(string message, params object[] args);

        void Fatal(string message, params object[] args);

        void ErrorException(string message, Exception exception);

        void ErrorException(Exception exception, string message, params object[] args);

        void WarnException(string message, Exception exception);

        void WarnException(Exception exception, string message, params object[] args);

        void Duration(string message, double durationMilliseconds);

        void SetProperty(string key, string value);

        string GetProperty(string key);

        string GetProperty(string key, string defaultValue);

        [System.Diagnostics.CodeAnalysis.SuppressMessage("Microsoft.Design", "CA1026:DefaultParametersShouldNotBeUsed")]
        IDisposable Context([CallerMemberName] string contextName = "");

        string Name { get; }
    }
}
