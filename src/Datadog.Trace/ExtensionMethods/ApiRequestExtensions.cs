using Datadog.Trace.Agent;

namespace Datadog.Trace.ExtensionMethods
{
    internal static class ApiRequestExtensions
    {
        public static void AddDefaultHeaders(this IApiRequest request)
        {
            request.AddHeader(AgentHttpHeaderNames.Language, ".NET");
            request.AddHeader(AgentHttpHeaderNames.TracerVersion, TracerConstants.AssemblyVersion);
            // don't add automatic instrumentation to requests from datadog code
            request.AddHeader(HttpHeaderNames.TracingEnabled, "false");
        }
    }
}
