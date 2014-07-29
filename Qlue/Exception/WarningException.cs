using System;

namespace Qlue
{
    /// <summary>
    /// Exception that is normal business logic error, should not raise error level
    /// </summary>
    [Serializable]
    public class WarningException : Exception
    {
        public WarningException()
        {
        }

        public WarningException(string message)
            : base(message)
        {
        }

        public WarningException(string message, Exception innerException)
            : base(message, innerException)
        {
        }

        protected WarningException(System.Runtime.Serialization.SerializationInfo info, System.Runtime.Serialization.StreamingContext context)
            : base(info, context)
        {
        }
    }
}
