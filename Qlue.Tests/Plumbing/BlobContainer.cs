using System;
using System.Collections.Generic;
using System.Collections.Concurrent;
using System.Linq;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using Moq;
using Qlue.Transport;

namespace Qlue.Tests.Plumbing
{
    internal class BlobContainer : IBlobContainer
    {
        private TestInstance testInstance;

        public BlobContainer(TestInstance testInstance)
        {
            this.testInstance = testInstance;
        }

        public IBlockBlob GetBlockBlobReference(string blobName)
        {
            var mockBlockBlob = new Mock<IBlockBlob>();

            mockBlockBlob.Setup(x => x.DeleteAsync())
                .Returns(Task.FromResult(false));

            mockBlockBlob.Setup(x => x.UploadFromStreamAsync(It.IsAny<Stream>()))
                .Callback<Stream>(x =>
                    {
                        byte[] blobData = new byte[x.Length];
                        x.Read(blobData, 0, blobData.Length);
                        this.testInstance.BlobStorage[blobName] = blobData;

                        this.testInstance.BlobPuts++;
                    })
                .Returns(Task.FromResult(false));

            mockBlockBlob.Setup(x => x.DownloadToStreamAsync(It.IsAny<Stream>()))
                .Callback<Stream>(x =>
                    {
                        byte[] blobData = this.testInstance.BlobStorage[blobName];

                        x.Write(blobData, 0, blobData.Length);

                        this.testInstance.BlobGets++;
                    })
                .Returns(Task.FromResult(false));

            return mockBlockBlob.Object;
        }

        public string Name
        {
            get { return "BLOB-CONTAINER"; }
        }
    }
}
