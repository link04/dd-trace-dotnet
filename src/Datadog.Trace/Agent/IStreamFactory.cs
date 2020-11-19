using System.IO;

namespace Datadog.Trace.Agent
{
    internal interface IStreamFactory
    {
        Stream GetBidirectionalStream();
    }
}
