using System;
using System.Runtime.Serialization;

namespace Qlue.DataReporting
{
    [Serializable]
    public class DataReportingException : Exception
    {
        public DataReportingException()
        {
        }

        public DataReportingException(string message)
            : base(message)
        {
        }

        public DataReportingException(string message, Exception innerException)
            : base(message, innerException)
        {
        }

        protected DataReportingException(SerializationInfo info, StreamingContext context)
            : base(info, context)
        {
        }
    }
}
