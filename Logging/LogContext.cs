using System;
using System.Diagnostics;
using System.Globalization;
using System.Runtime.CompilerServices;

namespace Qlue.Logging
{
    public sealed class LogContext : IDisposable
    {
        private ILog logger;
        private Stopwatch stopWatch;
        private IDisposable ndc;

        public LogContext(ILog logger, [CallerMemberName] string context = "", bool clearAsyncContext = false)
        {
            this.logger = logger;

            if (clearAsyncContext)
                AsyncContext.ClearAsyncData();

            this.ndc = AsyncContext.Push(this.logger.Name, context);
            this.stopWatch = Stopwatch.StartNew();

            // Log empty line to indicate start of function
            this.logger.Debug(string.Empty);
        }

        public void Dispose()
        {
            this.stopWatch.Stop();

            this.logger.Duration(
                string.Format(CultureInfo.InvariantCulture, "Duration {0:N1} ms", this.stopWatch.Elapsed.TotalMilliseconds),
                this.stopWatch.Elapsed.TotalMilliseconds);

            this.ndc.Dispose();
        }

        public static void ClearAsyncContext()
        {
            AsyncContext.ClearAsyncData();
        }
    }
}
