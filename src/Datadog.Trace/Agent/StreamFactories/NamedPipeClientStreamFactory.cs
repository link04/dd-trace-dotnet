using System.IO;
using System.IO.Pipes;
using System.Security.Principal;

namespace Datadog.Trace.Agent.StreamFactories
{
    internal class NamedPipeClientStreamFactory : IStreamFactory
    {
        private readonly string _pipeName;
        private readonly string _serverName;
        private readonly PipeOptions _pipeOptions;
        private readonly int _timeoutMs;
        private readonly TokenImpersonationLevel _impersonationLevel;

        public NamedPipeClientStreamFactory(string pipeName, int timeoutMs)
            : this(pipeName, ".", PipeOptions.Asynchronous, timeoutMs)
        {
        }

        public NamedPipeClientStreamFactory(string pipeName, string serverName, PipeOptions pipeOptions, int timeoutMs, TokenImpersonationLevel impersonationLevel = TokenImpersonationLevel.Identification)
        {
            _pipeName = pipeName;
            _serverName = serverName;
            _pipeOptions = pipeOptions;
            _timeoutMs = timeoutMs;
            _impersonationLevel = impersonationLevel;
        }

        public Stream GetBidirectionalStream()
        {
            var pipeStream = new NamedPipeClientStream(_serverName, _pipeName, PipeDirection.InOut, _pipeOptions, _impersonationLevel);
            pipeStream.Connect(_timeoutMs);
            return pipeStream;
        }
    }
}
