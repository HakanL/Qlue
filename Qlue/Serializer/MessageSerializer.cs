using System;
using System.IO;
using System.Runtime.Serialization;
using System.Xml;
using Qlue.Logging;

namespace Qlue
{
    public sealed class MessageSerializer : IMessageSerializer
    {
        public void Serialize(object instance, Stream target)
        {
            using (XmlDictionaryWriter xmlDictionaryWriter = XmlDictionaryWriter.CreateBinaryWriter(target, null, null, false))
            {
                XmlObjectSerializer xmlSerializer = GetXmlSerializer(instance.GetType());

                xmlSerializer.WriteObject(xmlDictionaryWriter, instance);

                xmlDictionaryWriter.Flush();
            }
        }

        public object Deserialize(Stream source, Type type)
        {
            object result;
            using (XmlDictionaryReader xmlDictionaryReader = XmlDictionaryReader.CreateBinaryReader(source, XmlDictionaryReaderQuotas.Max))
            {
                XmlObjectSerializer xmlSerializer = GetXmlSerializer(type);
                result = xmlSerializer.ReadObject(xmlDictionaryReader);
            }

            return result;
        }

        [System.Diagnostics.CodeAnalysis.SuppressMessage("Microsoft.Usage", "CA1801:ReviewUnusedParameters", MessageId = "type")]
        private static XmlObjectSerializer GetXmlSerializer(Type type)
        {
            //if (FrameworkUtility.GetDeclarativeAttribute<DataContractAttribute>(type) != null)
            //{
            //    return new DataContractSerializer(type);
            //}
            var serializer = new NetDataContractSerializer();

            serializer.AssemblyFormat = System.Runtime.Serialization.Formatters.FormatterAssemblyStyle.Simple;

            return serializer;
        }
    }
}
