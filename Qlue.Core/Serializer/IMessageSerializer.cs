using System;
using System.IO;

namespace Qlue
{
    public interface IMessageSerializer
    {
        void Serialize(object instance, Stream target);
        object Deserialize(Stream source, Type type);
    }
}
