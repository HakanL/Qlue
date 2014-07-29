using System;
using System.Collections.Generic;
using System.Globalization;
using Qlue.Logging;
using Qlue.Transport;

namespace Qlue
{
    public class BlobRepository
    {
        private ILog log;

        private readonly IBlobClient blobClient;
        private readonly Dictionary<string, IBlobContainer> blobContainers;

        public BlobRepository(ILog log, IBlobClient blobClient)
        {
            this.log = log;
            this.blobClient = blobClient;

            this.blobContainers = new Dictionary<string, IBlobContainer>();
        }

        public IBlobContainer GetBlobContainerForStoring()
        {
            return GetBlobContainer(this.blobClient.ContainerNameForStoring);
        }

        public IBlobContainer GetBlobContainer(string containerName)
        {
            containerName = containerName.ToLower(CultureInfo.InvariantCulture);
            lock (this.blobContainers)
            {
                IBlobContainer container;
                if (!this.blobContainers.TryGetValue(containerName, out container))
                {
                    container = this.blobClient.GetContainerReference(containerName);
                    this.blobContainers.Add(containerName, container);

                    log.Debug("Using container '{0}' for overflow storage", containerName);
                }

                return container;
            }
        }
    }
}
