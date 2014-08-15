using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Qlue.Transport
{
    public interface IBlobContainer
    {
        IBlockBlob GetBlockBlobReference(string blobName);
        string Name { get; }
    }
}
