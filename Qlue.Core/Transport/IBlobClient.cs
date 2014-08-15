using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Qlue.Transport
{
    public interface IBlobClient
    {
        IBlobContainer GetContainerReference(string containerName);
        string ContainerNameForStoring { get; }
    }
}
