using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Datadog.Trace.ClrProfiler.AutoInstrumentation.RabbitMQ
{
    internal class RabbitMQProducerTags : RabbitMQTags
    {
        public override string SpanKind => SpanKinds.Producer;
    }
}
