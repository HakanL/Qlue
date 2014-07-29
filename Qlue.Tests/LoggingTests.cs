using System;
using System.Collections.Generic;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Qlue.Logging;
using Moq;

namespace Qlue.Tests
{
    [TestClass]
    public class LoggingTests
    {
        private Plumbing.TestInstance testInstance = new Plumbing.TestInstance();

        [TestMethod]
        public void Set_And_Get_Property_Methods()
        {
            var log = testInstance.WireUpLogger();

            log.SetProperty("Test", "Value");
            Assert.AreEqual("Value", log.GetProperty("Test"));
            Assert.AreEqual("Default", log.GetProperty("Test2", "Default"));
            Assert.AreEqual("Value", log.GetProperty("Test", "Default"));
        }

        [TestMethod]
        public void Log_Trace_Level()
        {
            var log = testInstance.WireUpLogger();

            log.Trace("testTrace");
            Assert.AreEqual("TRACE", testInstance.LogEntries[0].Level);
            Assert.AreEqual("testTrace", testInstance.LogEntries[0].Message);
        }

        [TestMethod]
        public void Log_Debug_Level()
        {
            var log = testInstance.WireUpLogger();

            log.Debug("test");
            Assert.AreEqual("DEBUG", testInstance.LogEntries[0].Level);
            Assert.AreEqual("test", testInstance.LogEntries[0].Message);
        }

        [TestMethod]
        public void Log_Info_Level()
        {
            var log = testInstance.WireUpLogger();

            log.Info("testInfo");
            Assert.AreEqual("INFO", testInstance.LogEntries[0].Level);
            Assert.AreEqual("testInfo", testInstance.LogEntries[0].Message);
        }

        [TestMethod]
        public void Log_Warn_Level()
        {
            var log = testInstance.WireUpLogger();

            log.Warn("testWarn");
            Assert.AreEqual("WARN", testInstance.LogEntries[0].Level);
            Assert.AreEqual("testWarn", testInstance.LogEntries[0].Message);
        }

        [TestMethod]
        public void Log_Error_Level()
        {
            var log = testInstance.WireUpLogger();

            log.Error("testError");
            Assert.AreEqual("ERROR", testInstance.LogEntries[0].Level);
            Assert.AreEqual("testError", testInstance.LogEntries[0].Message);
        }

        [TestMethod]
        public void Log_Fatal_Level()
        {
            var log = testInstance.WireUpLogger();

            log.Fatal("testFatal");
            Assert.AreEqual("FATAL", testInstance.LogEntries[0].Level);
            Assert.AreEqual("testFatal", testInstance.LogEntries[0].Message);
        }

        [TestMethod]
        public void Log_Exception()
        {
            var log = testInstance.WireUpLogger();

            log.ErrorException("testException", new ArgumentNullException());
            Assert.AreEqual("ERROR", testInstance.LogEntries[0].Level);
            Assert.AreEqual("ArgumentNullException/testException", testInstance.LogEntries[0].Message);
        }

        [TestMethod]
        public void Log_Exception_Params()
        {
            var log = testInstance.WireUpLogger();

            log.ErrorException(new ArgumentNullException(), "testException#{0}", "test");
            Assert.AreEqual("ERROR", testInstance.LogEntries[0].Level);
            Assert.AreEqual("ArgumentNullException/testException#test", testInstance.LogEntries[0].Message);
        }

        [TestMethod]
        public void NLog_Normal_Log()
        {
            var logFactory = new NLogFactoryProvider();
            var tempPath = System.IO.Path.GetTempPath();
            logFactory.SetLogPath(tempPath.Substring(0, tempPath.Length - 1));

            var logger = logFactory.GetLogger("Logger");

            logger.ErrorException(new ArgumentException(), "Test");
            logger.Trace("test");
            logger.Debug("test");
            logger.Info("test");
            logger.Warn("test");
            logger.Error("test");
            logger.Fatal("test");

            logger.Duration("Test", 1234);
        }

        [TestMethod]
        public void NLog_Invalid_LogPath()
        {
            var logFactory = new NLogFactoryProvider();
            try
            {
                logFactory.SetLogPath(null);
                Assert.Fail("Should throw ArgumentNullException");
            }
            catch (ArgumentNullException)
            {
            }
        }

        [TestMethod]
        public void NullLog_Normal_Log()
        {
            var logFactory = new NullLogFactoryProvider();
            var tempPath = System.IO.Path.GetTempPath();
            logFactory.SetLogPath(tempPath.Substring(0, tempPath.Length - 1));

            var logger = logFactory.GetLogger("Logger");

            logger.ErrorException(new ArgumentException(), "Test");
            logger.Trace("test");
            logger.Debug("test");
            logger.Info("test");
            logger.Warn("test");
            logger.Error("test");
            logger.Fatal("test");

            logger.Duration("Test", 1234);
        }
    }
}
