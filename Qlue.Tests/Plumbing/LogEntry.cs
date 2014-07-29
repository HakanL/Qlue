using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Qlue.Tests.Plumbing
{
    public class LogEntry
    {
        public string Level { get; set; }
        public string Message { get; set; }

        public LogEntry(string level, string message)
        {
            this.Level = level;
            this.Message = message;
        }
    }
}
