using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.IO;

namespace Qlue.Transport
{
    public interface IBlockBlob
    {
        Task UploadFromStreamAsync(Stream payload);

        Task DownloadToStreamAsync(Stream payload);

        Task DeleteAsync();

        string ContentType { get; set; }

        Uri Uri { get; }
    }
}
