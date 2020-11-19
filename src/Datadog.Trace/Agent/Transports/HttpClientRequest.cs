#if NETCOREAPP
using System;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using Datadog.Trace.Agent.MessagePack;

namespace Datadog.Trace.Agent.Transports
{
    internal class HttpClientRequest : IApiRequest
    {
        private readonly HttpClient _client;
        private readonly HttpRequestMessage _request;
        private readonly CancellationTokenSource _cancellationTokenSource;

        public HttpClientRequest(HttpClient client, Uri endpoint)
        {
            _client = client;
            _cancellationTokenSource = new CancellationTokenSource();
            _request = new HttpRequestMessage(HttpMethod.Post, endpoint);
        }

        public void Cancel()
        {
            _cancellationTokenSource.Cancel();
        }

        public void AddHeader(string name, string value)
        {
            _request.Headers.Add(name, value);
        }

        public async Task<IApiResponse> PostAsync(Span[][] traces, FormatterResolverWrapper formatterResolver)
        {
            // re-create HttpContent on every retry because some versions of HttpClient always dispose of it, so we can't reuse.
            using (var content = new TracesMessagePackContent(traces, formatterResolver))
            {
                _request.Content = content;

                var response = await _client.SendAsync(_request, _cancellationTokenSource.Token).ConfigureAwait(false);

                return new HttpClientResponse(response);
            }
        }
    }
}
#endif
