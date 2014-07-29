using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Qlue.Tests
{
    public class TestMessage1
    {
        public string StringProp { get; set; }
        public int IntProp { get; set; }
        public byte[] Data { get; set; }
    }

    public class TestMessage2
    {
        public string StringProp { get; set; }
        public int IntProp { get; set; }
        public byte[] Data { get; set; }
    }

    public class TestNotifyMessage1
    {
        public string StringProp { get; set; }
        public int IntProp { get; set; }
        public byte[] Data { get; set; }
    }

    public class TestNotifyMessage2
    {
        public string StringProp { get; set; }
        public int IntProp { get; set; }
        public byte[] Data { get; set; }
    }

    [Serializable]
    public class TestException : Exception
    {
        public TestException(string message)
            : base(message, new InvalidProgramException("Test"))
        {
        }

        protected TestException(System.Runtime.Serialization.SerializationInfo info, System.Runtime.Serialization.StreamingContext context)
            : base(info, context)
        {
        }
    }

    // Note: Not serializable
    public class InvalidTestException : Exception
    {
        public InvalidTestException(string message)
            : base(message)
        {
        }
    }

    [Serializable]
    public class InvalidTestException2 : Exception
    {
        public InvalidTestException2(string message)
            : base(message)
        {
        }

        // Note: Missing constructor for serializer
    }
}
