using System;
using System.Collections.Generic;
using System.Collections.Concurrent;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Qlue.Logging;
using Moq;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Qlue.Tests.Plumbing
{
    internal class TestInstance
    {
        private ConcurrentDictionary<string, byte[]> blobStorage;
        private ConcurrentDictionary<string, QueueInstance> queues;
        private List<LogEntry> logEntries = new List<LogEntry>();
        private Dictionary<string, string> logKeyValue = new Dictionary<string, string>();
        private System.Threading.ManualResetEvent errorReceived;
        private System.Threading.ManualResetEvent warnReceived;
        private System.Threading.ManualResetEvent logTriggerReceived;
        private object lockObject = new object();

        public TestInstance()
        {
            this.blobStorage = new ConcurrentDictionary<string, byte[]>();
            this.queues = new ConcurrentDictionary<string, QueueInstance>();
            this.errorReceived = new System.Threading.ManualResetEvent(false);
            this.warnReceived = new System.Threading.ManualResetEvent(false);
            this.logTriggerReceived = new System.Threading.ManualResetEvent(false);
        }

        public IReadOnlyList<LogEntry> LogEntries
        {
            get { return this.logEntries.AsReadOnly(); }
        }

        public void ShouldHaveOne(string logLevel, string message)
        {
            lock (this.lockObject)
            {
                Assert.AreEqual(1, logEntries.Count(x => x.Level == logLevel && x.Message.Contains(message)));
            }
        }

        public string LogTriggerString { get; set; }

        public ConcurrentDictionary<string, byte[]> BlobStorage
        {
            get { return this.blobStorage; }
        }

        public ConcurrentDictionary<string, QueueInstance> Queues
        {
            get { return this.queues; }
        }

        public int BlobGets { get; set; }
        public int BlobPuts { get; set; }

        private void AddLogEntry(LogEntry logEntry)
        {
            lock (this.lockObject)
            {
                this.logEntries.Add(logEntry);

                if (logEntry.Level == "ERROR")
                    this.errorReceived.Set();
                if (logEntry.Level == "WARN")
                    this.warnReceived.Set();

                if (!string.IsNullOrEmpty(this.LogTriggerString) && logEntry.Message.Contains(this.LogTriggerString))
                    this.logTriggerReceived.Set();
            }
        }

        public ILog WireUpLogger()
        {
            var log = new Mock<ILog>();
            log.Setup(x => x.Trace(It.IsAny<string>()))
                .Callback<string, object[]>((x, y) => AddLogEntry(new LogEntry("TRACE", x)));
            log.Setup(x => x.Debug(It.IsAny<string>()))
                .Callback<string, object[]>((x, y) => AddLogEntry(new LogEntry("DEBUG", x)));
            log.Setup(x => x.Info(It.IsAny<string>()))
                .Callback<string, object[]>((x, y) => AddLogEntry(new LogEntry("INFO", x)));
            log.Setup(x => x.Warn(It.IsAny<string>()))
                .Callback<string, object[]>((x, y) => AddLogEntry(new LogEntry("WARN", x)));
            log.Setup(x => x.Error(It.IsAny<string>()))
                .Callback<string, object[]>((x, y) => AddLogEntry(new LogEntry("ERROR", x)));
            log.Setup(x => x.Fatal(It.IsAny<string>()))
                .Callback<string, object[]>((x, y) => AddLogEntry(new LogEntry("FATAL", x)));

            log.Setup(x => x.SetProperty(It.IsAny<string>(), It.IsAny<string>()))
                .Callback<string, string>((k, v) => this.logKeyValue.Add(k, v));
            log.Setup(x => x.GetProperty(It.IsAny<string>()))
                .Returns<string>(k => this.logKeyValue[k]);
            log.Setup(x => x.GetProperty(It.IsAny<string>(), It.IsAny<string>()))
                .Returns<string, string>((k, d) =>
                {
                    if (!this.logKeyValue.ContainsKey(k))
                        return d;
                    return this.logKeyValue[k];
                });
            log.SetupGet(x => x.Name).Returns("LoggerTestMoq");

            var logProvider = new Mock<ILogProvider>();
            logProvider.Setup(x => x.LogNormal(It.IsAny<string>(), It.IsAny<LogLevel>(), It.IsAny<string>(), It.IsAny<object[]>()))
                .Callback<string, LogLevel, string, object[]>((ndc, level, msg, p) => AddLogEntry(new LogEntry(level.ToString().ToUpper(), string.Format(msg, p))));
            logProvider.Setup(x => x.LogException(It.IsAny<string>(), It.IsAny<LogLevel>(), It.IsAny<Exception>(), It.IsAny<string>(), It.IsAny<object[]>()))
                .Callback<string, LogLevel, Exception, string, object[]>((ndc, level, ex, msg, p) =>
                    AddLogEntry(new LogEntry(level.ToString().ToUpper(), ex.GetType().Name + "/" + string.Format(msg, p))));
            logProvider.Setup(x => x.LogDuration(It.IsAny<string>(), It.IsAny<LogLevel>(), It.IsAny<string>(), It.IsAny<double>()))
                .Callback<string, LogLevel, string, double>((ndc, level, msg, duration) => AddLogEntry(new LogEntry(level.ToString().ToUpper(), duration.ToString("N0") + ":" + msg)));
            logProvider.SetupGet(x => x.Name).Returns("LogProviderTestMoq");

            var logger = new Log(logProvider.Object);

            return logger;
        }

        public void WaitForErrorReceived()
        {
            Assert.IsTrue(this.errorReceived.WaitOne(10000));
        }

        public void WaitForWarnReceived()
        {
            Assert.IsTrue(this.warnReceived.WaitOne(10000));
        }

        public void WaitForLogTriggerReceived()
        {
            Assert.IsTrue(this.logTriggerReceived.WaitOne(10000));
        }

        public ILogFactory WireUpLogFactory()
        {
            var logFactory = new Mock<ILogFactory>();

            logFactory.Setup(x => x.GetLogger(It.IsAny<string>())).Returns(WireUpLogger());

            return logFactory.Object;
        }
    }
}
