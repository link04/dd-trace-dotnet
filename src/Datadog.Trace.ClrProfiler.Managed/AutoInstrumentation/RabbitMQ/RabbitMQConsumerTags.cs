using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Datadog.Trace.ClrProfiler.AutoInstrumentation.RabbitMQ
{
    internal class RabbitMQConsumerTags : RabbitMQTags
    {
        public override string SpanKind => SpanKinds.Consumer;
    }
}
