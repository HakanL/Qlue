using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.IO;
using Microsoft.WindowsAzure.Storage;
using Microsoft.WindowsAzure.Storage.Auth;
using Microsoft.WindowsAzure.Storage.Blob;

namespace Qlue.Transport
{
    public class AzureBlockBlob : IBlockBlob
    {
        private CloudBlockBlob blockBlob;

        [CLSCompliant(false)]
        public AzureBlockBlob(CloudBlockBlob blockBlob)
        {
            this.blockBlob = blockBlob;
        }

        public Task UploadFromStreamAsync(Stream payload)
        {
            return Task.Factory.FromAsync<Stream>(blockBlob.BeginUploadFromStream, blockBlob.EndUploadFromStream, payload, null);
        }

        public Task DownloadToStreamAsync(Stream payload)
        {
            return Task.Factory.FromAsync<Stream>(blockBlob.BeginDownloadToStream, blockBlob.EndDownloadToStream, payload, null);
        }

        public Task DeleteAsync()
        {
            return blockBlob.DeleteAsync();
        }

        public string ContentType
        {
            get
            {
                return this.blockBlob.Properties.ContentType;
            }
            set
            {
                this.blockBlob.Properties.ContentType = value;
            }
        }

        public Uri Uri
        {
            get
            {
                return this.blockBlob.Uri;
            }
        }
    }
}
