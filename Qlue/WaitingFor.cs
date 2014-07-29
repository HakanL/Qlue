using System;
using System.Threading;
using System.Threading.Tasks;
using System.Diagnostics;
using Qlue.Logging;

namespace Qlue
{
    public class WaitingFor : IDisposable
    {
        private TaskCompletionSource<object> tcs;
        private CancellationTokenSource ct;
        private Stopwatch watch;
        private ILog log;

        public WaitingFor(TimeSpan timeout, ILog log)
        {
            this.log = log;

            this.tcs = new TaskCompletionSource<object>();

            this.ct = new CancellationTokenSource(timeout);
            this.ct.Token.Register(() => this.tcs.TrySetCanceled(), false);

            this.watch = Stopwatch.StartNew();
        }

        public Task<object> Task
        {
            get { return this.tcs.Task; }
        }

        public void Completed(object response)
        {
            this.watch.Stop();
            this.tcs.TrySetResult(response);
        }

        public TimeSpan ElapsedTime
        {
            get { return this.watch.Elapsed; }
        }

        public ILog Log
        {
            get { return this.log; }
        }

        public void Dispose()
        {
            Dispose(true);

            GC.SuppressFinalize(this);
        }

        protected virtual void Dispose(bool disposing)
        {
            if (disposing)
            {
                if (this.ct != null)
                {
                    this.ct.Dispose();
                    this.ct = null;
                }
            }
        }
    }
}
