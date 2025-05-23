// <copyright file="HttpClientRequest.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

#if NETCOREAPP
using System;
using System.IO;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Threading.Tasks;
using Datadog.Trace.AppSec;
using Datadog.Trace.HttpOverStreams;
using Datadog.Trace.Logging;

namespace Datadog.Trace.Agent.Transports
{
    internal class HttpClientRequest : IApiRequest
    {
        private static readonly IDatadogLogger Log = DatadogLogging.GetLoggerFor<HttpClientRequest>();

        private readonly HttpClient _client;
        private readonly HttpRequestMessage _postRequest;
        private readonly HttpRequestMessage _getRequest;
        private readonly Uri _uri;

        public HttpClientRequest(HttpClient client, Uri endpoint)
        {
            _client = client;
            _postRequest = new HttpRequestMessage(HttpMethod.Post, endpoint);
            _getRequest = new HttpRequestMessage(HttpMethod.Get, endpoint);
            _uri = endpoint;
        }

        public void AddHeader(string name, string value)
        {
            _postRequest.Headers.Add(name, value);
            _getRequest.Headers.Add(name, value);
        }

        public async Task<IApiResponse> GetAsync()
        {
            _getRequest.Content = null;

            return new HttpClientResponse(await _client.SendAsync(_getRequest).ConfigureAwait(false));
        }

        public Task<IApiResponse> PostAsync(ArraySegment<byte> bytes, string contentType)
            => PostAsync(bytes, contentType, null);

        public async Task<IApiResponse> PostAsync(ArraySegment<byte> bytes, string contentType, string contentEncoding)
        {
            // re-create HttpContent on every retry because some versions of HttpClient always dispose of it, so we can't reuse.
            using (var content = new ByteArrayContent(bytes.Array, bytes.Offset, bytes.Count))
            {
                content.Headers.ContentType = new MediaTypeHeaderValue(contentType);
                if (!string.IsNullOrEmpty(contentEncoding))
                {
                    content.Headers.ContentEncoding.Add(contentEncoding);
                }

                _postRequest.Content = content;

                var response = await _client.SendAsync(_postRequest).ConfigureAwait(false);

                return new HttpClientResponse(response);
            }
        }

        public async Task<IApiResponse> PostAsync(Func<Stream, Task> writeToRequestStream, string contentType, string contentEncoding, string multipartBoundary)
        {
            // re-create HttpContent on every retry because some versions of HttpClient always dispose of it, so we can't reuse.
            using var content = new PushStreamContent(writeToRequestStream);

            var contentTypeHeader = new MediaTypeHeaderValue(contentType);
            if (!string.IsNullOrEmpty(multipartBoundary))
            {
                contentTypeHeader.Parameters.Add(new NameValueHeaderValue("boundary", multipartBoundary));
            }

            content.Headers.ContentType = contentTypeHeader;

            if (!string.IsNullOrEmpty(contentEncoding))
            {
                content.Headers.ContentEncoding.Add(contentEncoding);
            }

            _postRequest.Content = content;
            var response = await _client.SendAsync(_postRequest).ConfigureAwait(false);

            return new HttpClientResponse(response);
        }

        public async Task<IApiResponse> PostAsync(MultipartFormItem[] items, MultipartCompression multipartCompression = MultipartCompression.None)
        {
            if (items is null)
            {
                ThrowHelper.ThrowArgumentNullException(nameof(items));
            }

            Log.Debug<int>("Sending multipart form request with {Count} items.", items.Length);

            using var formDataContent = new MultipartFormDataContent(boundary: DatadogHttpValues.Boundary);
            foreach (var item in items)
            {
                if (!item.IsValid(Log))
                {
                    continue;
                }

                HttpContent content = null;

                // Adds a form data item
                if (item.ContentInBytes is { } arraySegment)
                {
                    content = new ByteArrayContent(arraySegment.Array, arraySegment.Offset, arraySegment.Count);
                    Log.Debug("Adding to Multipart Byte Array | Name: {Name} | FileName: {FileName} | ContentType: {ContentType}", item.Name, item.FileName, item.ContentType);
                }
                else if (item.ContentInStream is { } stream)
                {
                    content = new StreamContent(stream);
                    Log.Debug("Adding to Multipart Stream | Name: {Name} | FileName: {FileName} | ContentType: {ContentType}", item.Name, item.FileName, item.ContentType);
                }
                else
                {
                    continue;
                }

                content.Headers.ContentType = new MediaTypeHeaderValue(item.ContentType);
                if (item.FileName is not null)
                {
                    formDataContent.Add(content, item.Name, item.FileName);
                }
                else
                {
                    formDataContent.Add(content, item.Name);
                }
            }

            if (multipartCompression == MultipartCompression.GZip)
            {
                Log.Debug("Using MultipartCompression.GZip");
                _postRequest.Content = new GzipCompressedContent(formDataContent);
            }
            else
            {
                _postRequest.Content = formDataContent;
            }

            var response = await _client.SendAsync(_postRequest).ConfigureAwait(false);
            return new HttpClientResponse(response);
        }
    }
}
#endif
