using System;
using System.Text;
using NLog;
using NLog.Config;
using NLog.LayoutRenderers;

namespace Qlue.Logging
{
    [LayoutRenderer("mdlc")]
    public class MdlcLayoutRenderer : LayoutRenderer
    {
        [RequiredParameter]
        [DefaultParameter]
        public string Item { get; set; }

        protected override void Append(StringBuilder builder, LogEventInfo logEvent)
        {
            string msg = Qlue.Logging.MappedDiagnosticsLogicalContext.Get(this.Item);
            builder.Append(msg);
        }
    }
}
