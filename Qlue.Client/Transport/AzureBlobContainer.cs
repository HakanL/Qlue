using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Microsoft.WindowsAzure.Storage;
using Microsoft.WindowsAzure.Storage.Auth;
using Microsoft.WindowsAzure.Storage.Blob;

namespace Qlue.Transport
{
    public class AzureBlobContainer : IBlobContainer
    {
        private CloudBlobContainer container;

        [CLSCompliant(false)]
        public AzureBlobContainer(CloudBlobContainer container)
        {
            this.container = container;
        }

        public IBlockBlob GetBlockBlobReference(string blobName)
        {
            var blockBlob = this.container.GetBlockBlobReference(blobName);

            return new AzureBlockBlob(blockBlob);
        }

        public string Name
        {
            get { return this.container.Name; }
        }
    }
}
