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
    public class AzureBlobClient : IBlobClient
    {
        private CloudBlobClient blobClient;
        private string containerNameForStoring;

        public AzureBlobClient(ICloudCredentials cloudCredentials, string containerNameForStoring)
        {
            this.containerNameForStoring = containerNameForStoring;

            var storageAccount = GetCloudStorageAccount(cloudCredentials);
            this.blobClient = storageAccount.CreateCloudBlobClient();
        }

        public void DeleteContainer(string containerName)
        {
            var container = this.blobClient.GetContainerReference(containerName);

            if (container.Exists())
                container.Delete();
        }

        private static CloudStorageAccount GetCloudStorageAccount(ICloudCredentials cloudCredentials)
        {
            var secret = Convert.FromBase64String(cloudCredentials.StorageAccountSecret);
            var storageCredentials = new StorageCredentials(cloudCredentials.StorageAccountName, secret);

            return new CloudStorageAccount(storageCredentials, true);
        }

        public IBlobContainer GetContainerReference(string containerName)
        {
            var container = this.blobClient.GetContainerReference(containerName);
            container.CreateIfNotExists();

            return new AzureBlobContainer(container);
        }

        public string ContainerNameForStoring
        {
            get { return this.containerNameForStoring; }
        }
    }
}
