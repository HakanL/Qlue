using System;
using System.IO;
using System.Runtime.Serialization;
using System.Runtime.Serialization.Formatters;

namespace Qlue
{
    [DataContract]
    public class ExceptionWrapper
    {
        [DataMember]
        private byte[] serializedException;

        [DataMember]
        public string ExceptionType { get; set; }

        [DataMember]
        public string Message { get; set; }

        [DataMember]
        public string StackTrace { get; set; }

        public ExceptionWrapper(Exception ex, bool omitStackTrace = false)
            : this(ex, omitStackTrace ? null : ex.StackTrace)
        {
        }

        public ExceptionWrapper(Exception ex, string stackTrace)
        {
            this.ExceptionType = ex.GetType().Name;
            this.Message = ex.Message;
            this.StackTrace = stackTrace;

            var serializer = GetSerializer();
            try
            {
                using (var ms = new MemoryStream())
                {
                    serializer.Serialize(ms, ex);

                    this.serializedException = ms.ToArray();
                }
            }
            catch (SerializationException)
            {
                // Failed to serialize the exception, use our standard exception instead
                var serviceException = new ServiceException(ex.Message);
                using (var ms = new MemoryStream())
                {
                    serializer.Serialize(ms, serviceException);

                    this.serializedException = ms.ToArray();
                }
            }
        }

        private static IFormatter GetSerializer()
        {
            var serializer = new System.Runtime.Serialization.Formatters.Binary.BinaryFormatter();
            serializer.AssemblyFormat = FormatterAssemblyStyle.Simple;
            serializer.TypeFormat = FormatterTypeStyle.TypesWhenNeeded;

            return serializer;
        }

        public Exception Unwrap()
        {
            try
            {
                var serializer = GetSerializer();
                using (var ms = new MemoryStream(this.serializedException))
                {
                    return (Exception)serializer.Deserialize(ms);
                }
            }
            catch (SerializationException)
            {
                return new ServiceException(this.Message);
            }
        }
    }
}
