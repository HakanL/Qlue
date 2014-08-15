using System;

namespace Qlue
{
    [Serializable]
    public class BusSendException : Exception
    {
        public BusSendException()
        {
        }

        public BusSendException(string message)
            : base(message)
        {
        }

        public BusSendException(string message, Exception innerException)
            : base(message, innerException)
        {
        }

        protected BusSendException(System.Runtime.Serialization.SerializationInfo info, System.Runtime.Serialization.StreamingContext context)
            : base(info, context)
        {
        }
    }
}
