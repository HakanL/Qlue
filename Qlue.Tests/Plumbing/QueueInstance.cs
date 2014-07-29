using System;
using System.Collections.Generic;
using System.Collections.Concurrent;
using System.Linq;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using Moq;
using Qlue.Transport;

namespace Qlue.Tests.Plumbing
{
    internal class QueueInstance
    {
        private object queueLock;
        private List<QueueObject> queue;
        private int dequeuePointer;
        private ManualResetEvent newItemInQueue;

        public QueueInstance()
        {
            this.queueLock = new object();
            this.queue = new List<QueueObject>();
            this.dequeuePointer = -1;
            this.newItemInQueue = new ManualResetEvent(false);
        }

        public QueueObject this[int index]
        {
            get
            {
                lock (queueLock)
                {
                    return this.queue[index];
                }
            }
        }

        public void Add(Pipeline.PipelineContext context)
        {
            lock (queueLock)
            {
                var newQueueObject = new QueueObject(context);
                this.queue.Add(newQueueObject);

                this.newItemInQueue.Set();
            }
        }

        public QueueObject Dequeue()
        {
            lock (this.queueLock)
            {
                this.newItemInQueue.Reset();

                if (this.dequeuePointer + 1 >= queue.Count)
                    throw new Exception("Nothing new in the queue");

                var queueObject = queue[++this.dequeuePointer];

                return queueObject;
            }
        }

        public bool WaitForNewItemInQueue(int millisecondsTimeout = 10000)
        {
            return this.newItemInQueue.WaitOne(millisecondsTimeout);
        }

        public int Count
        {
            get { return this.queue.Count; }
        }
    }
}
