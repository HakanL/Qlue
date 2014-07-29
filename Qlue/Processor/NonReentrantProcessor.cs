using System;
using System.Collections.Generic;
using Qlue.Logging;
using Qlue.Transport;

namespace Qlue.Processor
{
    public abstract class NonReentrantProcessor<T> : RobustQueueProcessor<T>
    {
        private HashSet<string> activeProcesses;
        private ILog log;

        protected NonReentrantProcessor(
            ILog log,
            IStorageQueue storageQueue)
            : base(log, storageQueue)
        {
            this.log = log;

            this.activeProcesses = new HashSet<string>();
        }

        protected abstract string GetProcessName(T message);

        protected override bool ShouldProcessMessage(T message)
        {
            string processName = GetProcessName(message);

            lock (this.activeProcesses)
            {
                if (this.activeProcesses.Contains(processName))
                {
                    this.log.Warn("Already running process {0}, skipping this message", processName);

                    return false;
                }

                this.activeProcesses.Add(processName);
            }

            return base.ShouldProcessMessage(message);
        }

        protected override void MessageCompleted(T message)
        {
            string processName = GetProcessName(message);

            lock (this.activeProcesses)
            {
                if (this.activeProcesses.Contains(processName))
                    this.activeProcesses.Remove(processName);
            }

            base.MessageCompleted(message);
        }
    }
}
