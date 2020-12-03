using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Datadog.Trace.ClrProfiler.AutoInstrumentation.RabbitMQ
{
    /// <summary>
    /// BasicGetResult interface for ducktyping
    /// </summary>
    public interface IBasicGetResult
    {
        /// <summary>
        /// Gets the message body of the result
        /// </summary>
        IBody Body { get; }
    }
}
