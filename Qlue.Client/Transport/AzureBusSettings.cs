using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Qlue.Transport
{
    public class AzureBusSettings
    {
        public bool Express { get; set; }

        public TimeSpan AutoDeleteOnIdle { get; set; }

        public bool UseAmqp { get; set; }

        public bool Partitioning { get; set; }
    }
}
