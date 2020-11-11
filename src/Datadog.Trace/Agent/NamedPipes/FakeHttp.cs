using System;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Datadog.Trace.Agent.MessagePack;

namespace Datadog.Trace.Agent.NamedPipes
{
    internal static class FakeHttp
    {
        private static readonly FormatterResolverWrapper FormatterResolver = new FormatterResolverWrapper(SpanFormatterResolver.Instance);

        public static async Task<byte[]> CreatePost(TraceRequest request)
        {
            var headers = string.Join(Environment.NewLine, request.Headers.Select(h => $"{h.Key}: {h.Value}"));
            var traceBytes = await CachedSerializer.Instance.SerializeToByteArray(request.Traces, FormatterResolver);

            var sentinelIndex = 0;
            var contiguousNullBitsAbove6 = 0;
            var lineFeeds = 0;
            var contiguousNullBits = 0;

            while (contiguousNullBits < 8)
            {
                sentinelIndex++;

                if (traceBytes[sentinelIndex] == 0x00)
                {
                    contiguousNullBits++;
                }
                else
                {
                    contiguousNullBits = 0;
                }

                if (contiguousNullBits > 6)
                {
                    contiguousNullBitsAbove6++;
                }

                if (traceBytes[sentinelIndex] == 0x0a)
                {
                    lineFeeds++;
                }
            }

            var trimmedContent = new byte[sentinelIndex + 1 - contiguousNullBitsAbove6 - lineFeeds];
            contiguousNullBits = 0;
            var offset = 0;

            for (var i = 0; i < trimmedContent.Length; i++)
            {
                if (traceBytes[i + offset] == 0x0a)
                {
                    offset++;
                    i--;
                    continue;
                }

                if (traceBytes[i + offset] == 0x00)
                {
                    contiguousNullBits++;
                }
                else
                {
                    contiguousNullBits = 0;
                }

                if (contiguousNullBits >= 6)
                {
                    // Do not copy anything past 6 bits
                    offset++;
                    i--;
                    continue;
                }

                trimmedContent[i] = traceBytes[i + offset];
            }

            var remappedLength = trimmedContent.Length.ToString("X");

            var headersOnly = $@"{request.Method} {request.Path} HTTP/{request.Version}
Host: {request.Host}
{headers}

{remappedLength}
";

            Encoding iso = Encoding.GetEncoding("ISO-8859-1");
            var messageBeginBytes = new byte[]
            {
                0x02, 0x00, 0x00, 0x00, 0x45, 0x00, 0x0a, 0xfa, 0x75, 0x9e, 0x40, 0x00, 0x80, 0x06, 0x00, 0x00,
                0x7f, 0x00, 0x00, 0x01, 0x7f, 0x00, 0x00, 0x01, 0xe3, 0x04, 0x1f, 0xbe, 0x4c, 0x41, 0x64, 0x9c,
                0x3f, 0x60, 0x91, 0x80, 0x50, 0x18, 0x27, 0xf5, 0x70, 0xc1, 0x00, 0x00
            };
            var headersOnlyBytes = iso.GetBytes(headersOnly);
            var messageEndBytes = new byte[] { 0x0d, 0x0a, 0x30, 0x0d, 0x0a, 0x0d, 0x0a };

            var length = messageBeginBytes.Length + headersOnlyBytes.Length + trimmedContent.Length + messageEndBytes.Length;

            var messageBuffer = new byte[length];

            var bufferOffset = 0;
            CopyToBuffer(ref bufferOffset, ref messageBuffer, messageBeginBytes);
            CopyToBuffer(ref bufferOffset, ref messageBuffer, headersOnlyBytes);
            CopyToBuffer(ref bufferOffset, ref messageBuffer, trimmedContent);
            CopyToBuffer(ref bufferOffset, ref messageBuffer, messageEndBytes);

            var id = Guid.NewGuid();
            var basePath = $"C:\\_INSPECTION\\FakeHttp\\{id}";
            System.IO.Directory.CreateDirectory(basePath);
            // This payload
            System.IO.File.WriteAllText(System.IO.Path.Combine(basePath, $"http-text.txt"), Encoding.Default.GetString(messageBuffer));
            // An example payload
            // System.IO.File.WriteAllText(System.IO.Path.Combine(basePath, $"wireshark-example.txt"), Encoding.Default.GetString(GetExample()));

            return messageBuffer;
        }

        public static void ReadResponse(TraceResponse response, string responseText)
        {
            if (string.IsNullOrWhiteSpace(responseText))
            {
                // TODO: For mock testing, to remove
                response.Body = @"{'rate_by_service':1}";
                response.ContentLength = response.Body.Length;
                response.StatusCode = 200;

                return;
            }

            var lines = responseText.Split(new[] { Environment.NewLine }, StringSplitOptions.None);
            var foundEmptyLine = false;
            var remainder = string.Empty;

            foreach (var line in lines)
            {
                if (foundEmptyLine)
                {
                    remainder += line;
                }
                else if (string.IsNullOrWhiteSpace(line))
                {
                    foundEmptyLine = true;
                }
                else if (line.Contains("HTTP/"))
                {
                    ParseStatusLine(response, line);
                }
            }

            // TODO: We may need to deserialize?
            response.Body = remainder;
        }

        private static void ParseStatusLine(TraceResponse response, string line)
        {
            const int MinStatusLineLength = 12; // "HTTP/1.x 123"
            if (line.Length < MinStatusLineLength || line[8] != ' ')
            {
                throw new Exception("Invalid response, expecting HTTP/1.0 or 1.1, was:" + line);
            }

            if (!line.StartsWith("HTTP/1."))
            {
                throw new Exception("Invalid response, expecting HTTP/1.0 or 1.1, was:" + line);
            }

            // response.Version = _httpVersion;
            // Set the status code
            if (int.TryParse(line.Substring(9, 3), out int statusCode))
            {
                response.StatusCode = statusCode;
            }
            else
            {
                throw new Exception("Invalid response, can't parse status code. Line was:" + line);
            }

            // Parse (optional) reason phrase
            if (line.Length == MinStatusLineLength)
            {
                response.ReasonPhrase = string.Empty;
            }
            else if (line[MinStatusLineLength] == ' ')
            {
                response.ReasonPhrase = line.Substring(MinStatusLineLength + 1);
            }
            else
            {
                throw new Exception("Invalid response");
            }
        }

        private static void CopyToBuffer(ref int offset, ref byte[] messageBuffer, byte[] messagePartial)
        {
            for (var index = 0; index < messagePartial.Length; index++)
            {
                messageBuffer[offset + index] = messagePartial[index];
            }

            offset += messagePartial.Length;
        }

        private static byte[] GetExample()
        {
            var example = new byte[]
            {
                0x02, 0x00, 0x00, 0x00, 0x45, 0x00, 0x0a, 0xfa, 0x75, 0x9e, 0x40, 0x00, 0x80, 0x06, 0x00, 0x00,
                0x7f, 0x00, 0x00, 0x01, 0x7f, 0x00, 0x00, 0x01, 0xe3, 0x04, 0x1f, 0xbe, 0x4c, 0x41, 0x64, 0x9c,
                0x3f, 0x60, 0x91, 0x80, 0x50, 0x18, 0x27, 0xf5, 0x70, 0xc1, 0x00, 0x00, 0x50, 0x4f, 0x53, 0x54,
                0x20, 0x2f, 0x76, 0x30, 0x2e, 0x34, 0x2f, 0x74, 0x72, 0x61, 0x63, 0x65, 0x73, 0x20, 0x48, 0x54,
                0x54, 0x50, 0x2f, 0x31, 0x2e, 0x31, 0x0d, 0x0a, 0x48, 0x6f, 0x73, 0x74, 0x3a, 0x20, 0x31, 0x32,
                0x37, 0x2e, 0x30, 0x2e, 0x30, 0x2e, 0x31, 0x3a, 0x38, 0x31, 0x32, 0x36, 0x0d, 0x0a, 0x58, 0x2d,
                0x44, 0x61, 0x74, 0x61, 0x64, 0x6f, 0x67, 0x2d, 0x54, 0x72, 0x61, 0x63, 0x65, 0x2d, 0x43, 0x6f,
                0x75, 0x6e, 0x74, 0x3a, 0x20, 0x36, 0x0d, 0x0a, 0x44, 0x61, 0x74, 0x61, 0x64, 0x6f, 0x67, 0x2d,
                0x4d, 0x65, 0x74, 0x61, 0x2d, 0x4c, 0x61, 0x6e, 0x67, 0x2d, 0x49, 0x6e, 0x74, 0x65, 0x72, 0x70,
                0x72, 0x65, 0x74, 0x65, 0x72, 0x3a, 0x20, 0x2e, 0x4e, 0x45, 0x54, 0x20, 0x43, 0x6f, 0x72, 0x65,
                0x0d, 0x0a, 0x44, 0x61, 0x74, 0x61, 0x64, 0x6f, 0x67, 0x2d, 0x4d, 0x65, 0x74, 0x61, 0x2d, 0x4c,
                0x61, 0x6e, 0x67, 0x2d, 0x56, 0x65, 0x72, 0x73, 0x69, 0x6f, 0x6e, 0x3a, 0x20, 0x33, 0x2e, 0x31,
                0x2e, 0x38, 0x0d, 0x0a, 0x44, 0x61, 0x74, 0x61, 0x64, 0x6f, 0x67, 0x2d, 0x4d, 0x65, 0x74, 0x61,
                0x2d, 0x4c, 0x61, 0x6e, 0x67, 0x3a, 0x20, 0x2e, 0x4e, 0x45, 0x54, 0x0d, 0x0a, 0x44, 0x61, 0x74,
                0x61, 0x64, 0x6f, 0x67, 0x2d, 0x4d, 0x65, 0x74, 0x61, 0x2d, 0x54, 0x72, 0x61, 0x63, 0x65, 0x72,
                0x2d, 0x56, 0x65, 0x72, 0x73, 0x69, 0x6f, 0x6e, 0x3a, 0x20, 0x31, 0x2e, 0x31, 0x39, 0x2e, 0x36,
                0x2e, 0x30, 0x0d, 0x0a, 0x78, 0x2d, 0x64, 0x61, 0x74, 0x61, 0x64, 0x6f, 0x67, 0x2d, 0x74, 0x72,
                0x61, 0x63, 0x69, 0x6e, 0x67, 0x2d, 0x65, 0x6e, 0x61, 0x62, 0x6c, 0x65, 0x64, 0x3a, 0x20, 0x66,
                0x61, 0x6c, 0x73, 0x65, 0x0d, 0x0a, 0x54, 0x72, 0x61, 0x6e, 0x73, 0x66, 0x65, 0x72, 0x2d, 0x45,
                0x6e, 0x63, 0x6f, 0x64, 0x69, 0x6e, 0x67, 0x3a, 0x20, 0x63, 0x68, 0x75, 0x6e, 0x6b, 0x65, 0x64,
                0x0d, 0x0a, 0x43, 0x6f, 0x6e, 0x74, 0x65, 0x6e, 0x74, 0x2d, 0x54, 0x79, 0x70, 0x65, 0x3a, 0x20,
                0x61, 0x70, 0x70, 0x6c, 0x69, 0x63, 0x61, 0x74, 0x69, 0x6f, 0x6e, 0x2f, 0x6d, 0x73, 0x67, 0x70,
                0x61, 0x63, 0x6b, 0x0d, 0x0a, 0x0d, 0x0a, 0x39, 0x38, 0x42, 0x0d, 0x0a, 0x96, 0x91, 0x8a, 0xa8,
                0x74, 0x72, 0x61, 0x63, 0x65, 0x5f, 0x69, 0x64, 0xcf, 0x01, 0x3f, 0x4b, 0x48, 0xc5, 0xcd, 0xd8,
                0xc6, 0xa7, 0x73, 0x70, 0x61, 0x6e, 0x5f, 0x69, 0x64, 0xcf, 0x25, 0xe3, 0x7f, 0x95, 0x59, 0x42,
                0xa1, 0x8f, 0xa4, 0x6e, 0x61, 0x6d, 0x65, 0xb3, 0x61, 0x73, 0x70, 0x6e, 0x65, 0x74, 0x5f, 0x63,
                0x6f, 0x72, 0x65, 0x2e, 0x72, 0x65, 0x71, 0x75, 0x65, 0x73, 0x74, 0xa8, 0x72, 0x65, 0x73, 0x6f,
                0x75, 0x72, 0x63, 0x65, 0xae, 0x47, 0x45, 0x54, 0x20, 0x48, 0x6f, 0x6d, 0x65, 0x2f, 0x49, 0x6e,
                0x64, 0x65, 0x78, 0xa7, 0x73, 0x65, 0x72, 0x76, 0x69, 0x63, 0x65, 0xb7, 0x53, 0x61, 0x6d, 0x70,
                0x6c, 0x65, 0x73, 0x2e, 0x41, 0x73, 0x70, 0x4e, 0x65, 0x74, 0x43, 0x6f, 0x72, 0x65, 0x4d, 0x76,
                0x63, 0x33, 0x31, 0xa4, 0x74, 0x79, 0x70, 0x65, 0xa3, 0x77, 0x65, 0x62, 0xa5, 0x73, 0x74, 0x61,
                0x72, 0x74, 0xcf, 0x16, 0x43, 0xc9, 0xd0, 0x21, 0x46, 0x02, 0x98, 0xa8, 0x64, 0x75, 0x72, 0x61,
                0x74, 0x69, 0x6f, 0x6e, 0xce, 0x00, 0x15, 0x69, 0x40, 0xa4, 0x6d, 0x65, 0x74, 0x61, 0x89, 0xa3,
                0x65, 0x6e, 0x76, 0xb3, 0x63, 0x6f, 0x6c, 0x69, 0x6e, 0x2d, 0x6c, 0x6f, 0x63, 0x61, 0x6c, 0x2d,
                0x6f, 0x72, 0x63, 0x68, 0x61, 0x72, 0x64, 0xa7, 0x76, 0x65, 0x72, 0x73, 0x69, 0x6f, 0x6e, 0xa5,
                0x31, 0x2e, 0x30, 0x2e, 0x30, 0xa9, 0x73, 0x70, 0x61, 0x6e, 0x2e, 0x6b, 0x69, 0x6e, 0x64, 0xa6,
                0x73, 0x65, 0x72, 0x76, 0x65, 0x72, 0xb0, 0x68, 0x74, 0x74, 0x70, 0x2e, 0x73, 0x74, 0x61, 0x74,
                0x75, 0x73, 0x5f, 0x63, 0x6f, 0x64, 0x65, 0xa3, 0x32, 0x30, 0x30, 0xab, 0x68, 0x74, 0x74, 0x70,
                0x2e, 0x6d, 0x65, 0x74, 0x68, 0x6f, 0x64, 0xa3, 0x47, 0x45, 0x54, 0xb9, 0x68, 0x74, 0x74, 0x70,
                0x2e, 0x72, 0x65, 0x71, 0x75, 0x65, 0x73, 0x74, 0x2e, 0x68, 0x65, 0x61, 0x64, 0x65, 0x72, 0x73,
                0x2e, 0x68, 0x6f, 0x73, 0x74, 0xaf, 0x6c, 0x6f, 0x63, 0x61, 0x6c, 0x68, 0x6f, 0x73, 0x74, 0x3a,
                0x35, 0x34, 0x35, 0x36, 0x34, 0xa8, 0x68, 0x74, 0x74, 0x70, 0x2e, 0x75, 0x72, 0x6c, 0xb7, 0x68,
                0x74, 0x74, 0x70, 0x3a, 0x2f, 0x2f, 0x6c, 0x6f, 0x63, 0x61, 0x6c, 0x68, 0x6f, 0x73, 0x74, 0x3a,
                0x35, 0x34, 0x35, 0x36, 0x34, 0x2f, 0xa8, 0x6c, 0x61, 0x6e, 0x67, 0x75, 0x61, 0x67, 0x65, 0xa6,
                0x64, 0x6f, 0x74, 0x6e, 0x65, 0x74, 0xa9, 0x63, 0x6f, 0x6d, 0x70, 0x6f, 0x6e, 0x65, 0x6e, 0x74,
                0xab, 0x61, 0x73, 0x70, 0x6e, 0x65, 0x74, 0x5f, 0x63, 0x6f, 0x72, 0x65, 0xa7, 0x6d, 0x65, 0x74,
                0x72, 0x69, 0x63, 0x73, 0x81, 0xb5, 0x5f, 0x73, 0x61, 0x6d, 0x70, 0x6c, 0x69, 0x6e, 0x67, 0x5f,
                0x70, 0x72, 0x69, 0x6f, 0x72, 0x69, 0x74, 0x79, 0x5f, 0x76, 0x31, 0xcb, 0x3f, 0xf0, 0x00, 0x00,
                0x00, 0x00, 0x00, 0x00, 0x91, 0x8a, 0xa8, 0x74, 0x72, 0x61, 0x63, 0x65, 0x5f, 0x69, 0x64, 0xcf,
                0x7d, 0x5f, 0xfa, 0xed, 0x9f, 0xc1, 0x37, 0x0f, 0xa7, 0x73, 0x70, 0x61, 0x6e, 0x5f, 0x69, 0x64,
                0xcf, 0x34, 0x2f, 0xdd, 0x41, 0x9f, 0x72, 0x25, 0x44, 0xa4, 0x6e, 0x61, 0x6d, 0x65, 0xb3, 0x61,
                0x73, 0x70, 0x6e, 0x65, 0x74, 0x5f, 0x63, 0x6f, 0x72, 0x65, 0x2e, 0x72, 0x65, 0x71, 0x75, 0x65,
                0x73, 0x74, 0xa8, 0x72, 0x65, 0x73, 0x6f, 0x75, 0x72, 0x63, 0x65, 0xae, 0x47, 0x45, 0x54, 0x20,
                0x48, 0x6f, 0x6d, 0x65, 0x2f, 0x49, 0x6e, 0x64, 0x65, 0x78, 0xa7, 0x73, 0x65, 0x72, 0x76, 0x69,
                0x63, 0x65, 0xb7, 0x53, 0x61, 0x6d, 0x70, 0x6c, 0x65, 0x73, 0x2e, 0x41, 0x73, 0x70, 0x4e, 0x65,
                0x74, 0x43, 0x6f, 0x72, 0x65, 0x4d, 0x76, 0x63, 0x33, 0x31, 0xa4, 0x74, 0x79, 0x70, 0x65, 0xa3,
                0x77, 0x65, 0x62, 0xa5, 0x73, 0x74, 0x61, 0x72, 0x74, 0xcf, 0x16, 0x43, 0xc9, 0xd0, 0x29, 0xc1,
                0xa4, 0x7c, 0xa8, 0x64, 0x75, 0x72, 0x61, 0x74, 0x69, 0x6f, 0x6e, 0xce, 0x00, 0x14, 0xa7, 0x1c,
                0xa4, 0x6d, 0x65, 0x74, 0x61, 0x89, 0xa3, 0x65, 0x6e, 0x76, 0xb3, 0x63, 0x6f, 0x6c, 0x69, 0x6e,
                0x2d, 0x6c, 0x6f, 0x63, 0x61, 0x6c, 0x2d, 0x6f, 0x72, 0x63, 0x68, 0x61, 0x72, 0x64, 0xa7, 0x76,
                0x65, 0x72, 0x73, 0x69, 0x6f, 0x6e, 0xa5, 0x31, 0x2e, 0x30, 0x2e, 0x30, 0xa9, 0x73, 0x70, 0x61,
                0x6e, 0x2e, 0x6b, 0x69, 0x6e, 0x64, 0xa6, 0x73, 0x65, 0x72, 0x76, 0x65, 0x72, 0xb0, 0x68, 0x74,
                0x74, 0x70, 0x2e, 0x73, 0x74, 0x61, 0x74, 0x75, 0x73, 0x5f, 0x63, 0x6f, 0x64, 0x65, 0xa3, 0x32,
                0x30, 0x30, 0xab, 0x68, 0x74, 0x74, 0x70, 0x2e, 0x6d, 0x65, 0x74, 0x68, 0x6f, 0x64, 0xa3, 0x47,
                0x45, 0x54, 0xb9, 0x68, 0x74, 0x74, 0x70, 0x2e, 0x72, 0x65, 0x71, 0x75, 0x65, 0x73, 0x74, 0x2e,
                0x68, 0x65, 0x61, 0x64, 0x65, 0x72, 0x73, 0x2e, 0x68, 0x6f, 0x73, 0x74, 0xaf, 0x6c, 0x6f, 0x63,
                0x61, 0x6c, 0x68, 0x6f, 0x73, 0x74, 0x3a, 0x35, 0x34, 0x35, 0x36, 0x34, 0xa8, 0x68, 0x74, 0x74,
                0x70, 0x2e, 0x75, 0x72, 0x6c, 0xb7, 0x68, 0x74, 0x74, 0x70, 0x3a, 0x2f, 0x2f, 0x6c, 0x6f, 0x63,
                0x61, 0x6c, 0x68, 0x6f, 0x73, 0x74, 0x3a, 0x35, 0x34, 0x35, 0x36, 0x34, 0x2f, 0xa8, 0x6c, 0x61,
                0x6e, 0x67, 0x75, 0x61, 0x67, 0x65, 0xa6, 0x64, 0x6f, 0x74, 0x6e, 0x65, 0x74, 0xa9, 0x63, 0x6f,
                0x6d, 0x70, 0x6f, 0x6e, 0x65, 0x6e, 0x74, 0xab, 0x61, 0x73, 0x70, 0x6e, 0x65, 0x74, 0x5f, 0x63,
                0x6f, 0x72, 0x65, 0xa7, 0x6d, 0x65, 0x74, 0x72, 0x69, 0x63, 0x73, 0x81, 0xb5, 0x5f, 0x73, 0x61,
                0x6d, 0x70, 0x6c, 0x69, 0x6e, 0x67, 0x5f, 0x70, 0x72, 0x69, 0x6f, 0x72, 0x69, 0x74, 0x79, 0x5f,
                0x76, 0x31, 0xcb, 0x3f, 0xf0, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x91, 0x8a, 0xa8, 0x74, 0x72,
                0x61, 0x63, 0x65, 0x5f, 0x69, 0x64, 0xcf, 0x03, 0x71, 0x33, 0x63, 0xf9, 0x33, 0xbc, 0xfe, 0xa7,
                0x73, 0x70, 0x61, 0x6e, 0x5f, 0x69, 0x64, 0xcf, 0x6a, 0xe1, 0xa0, 0xc8, 0xb4, 0xb0, 0x82, 0xee,
                0xa4, 0x6e, 0x61, 0x6d, 0x65, 0xb3, 0x61, 0x73, 0x70, 0x6e, 0x65, 0x74, 0x5f, 0x63, 0x6f, 0x72,
                0x65, 0x2e, 0x72, 0x65, 0x71, 0x75, 0x65, 0x73, 0x74, 0xa8, 0x72, 0x65, 0x73, 0x6f, 0x75, 0x72,
                0x63, 0x65, 0xae, 0x47, 0x45, 0x54, 0x20, 0x48, 0x6f, 0x6d, 0x65, 0x2f, 0x49, 0x6e, 0x64, 0x65,
                0x78, 0xa7, 0x73, 0x65, 0x72, 0x76, 0x69, 0x63, 0x65, 0xb7, 0x53, 0x61, 0x6d, 0x70, 0x6c, 0x65,
                0x73, 0x2e, 0x41, 0x73, 0x70, 0x4e, 0x65, 0x74, 0x43, 0x6f, 0x72, 0x65, 0x4d, 0x76, 0x63, 0x33,
                0x31, 0xa4, 0x74, 0x79, 0x70, 0x65, 0xa3, 0x77, 0x65, 0x62, 0xa5, 0x73, 0x74, 0x61, 0x72, 0x74,
                0xcf, 0x16, 0x43, 0xc9, 0xd0, 0x32, 0x43, 0x57, 0x1c, 0xa8, 0x64, 0x75, 0x72, 0x61, 0x74, 0x69,
                0x6f, 0x6e, 0xce, 0x00, 0x17, 0x40, 0x58, 0xa4, 0x6d, 0x65, 0x74, 0x61, 0x89, 0xa3, 0x65, 0x6e,
                0x76, 0xb3, 0x63, 0x6f, 0x6c, 0x69, 0x6e, 0x2d, 0x6c, 0x6f, 0x63, 0x61, 0x6c, 0x2d, 0x6f, 0x72,
                0x63, 0x68, 0x61, 0x72, 0x64, 0xa7, 0x76, 0x65, 0x72, 0x73, 0x69, 0x6f, 0x6e, 0xa5, 0x31, 0x2e,
                0x30, 0x2e, 0x30, 0xa9, 0x73, 0x70, 0x61, 0x6e, 0x2e, 0x6b, 0x69, 0x6e, 0x64, 0xa6, 0x73, 0x65,
                0x72, 0x76, 0x65, 0x72, 0xb0, 0x68, 0x74, 0x74, 0x70, 0x2e, 0x73, 0x74, 0x61, 0x74, 0x75, 0x73,
                0x5f, 0x63, 0x6f, 0x64, 0x65, 0xa3, 0x32, 0x30, 0x30, 0xab, 0x68, 0x74, 0x74, 0x70, 0x2e, 0x6d,
                0x65, 0x74, 0x68, 0x6f, 0x64, 0xa3, 0x47, 0x45, 0x54, 0xb9, 0x68, 0x74, 0x74, 0x70, 0x2e, 0x72,
                0x65, 0x71, 0x75, 0x65, 0x73, 0x74, 0x2e, 0x68, 0x65, 0x61, 0x64, 0x65, 0x72, 0x73, 0x2e, 0x68,
                0x6f, 0x73, 0x74, 0xaf, 0x6c, 0x6f, 0x63, 0x61, 0x6c, 0x68, 0x6f, 0x73, 0x74, 0x3a, 0x35, 0x34,
                0x35, 0x36, 0x34, 0xa8, 0x68, 0x74, 0x74, 0x70, 0x2e, 0x75, 0x72, 0x6c, 0xb7, 0x68, 0x74, 0x74,
                0x70, 0x3a, 0x2f, 0x2f, 0x6c, 0x6f, 0x63, 0x61, 0x6c, 0x68, 0x6f, 0x73, 0x74, 0x3a, 0x35, 0x34,
                0x35, 0x36, 0x34, 0x2f, 0xa8, 0x6c, 0x61, 0x6e, 0x67, 0x75, 0x61, 0x67, 0x65, 0xa6, 0x64, 0x6f,
                0x74, 0x6e, 0x65, 0x74, 0xa9, 0x63, 0x6f, 0x6d, 0x70, 0x6f, 0x6e, 0x65, 0x6e, 0x74, 0xab, 0x61,
                0x73, 0x70, 0x6e, 0x65, 0x74, 0x5f, 0x63, 0x6f, 0x72, 0x65, 0xa7, 0x6d, 0x65, 0x74, 0x72, 0x69,
                0x63, 0x73, 0x81, 0xb5, 0x5f, 0x73, 0x61, 0x6d, 0x70, 0x6c, 0x69, 0x6e, 0x67, 0x5f, 0x70, 0x72,
                0x69, 0x6f, 0x72, 0x69, 0x74, 0x79, 0x5f, 0x76, 0x31, 0xcb, 0x3f, 0xf0, 0x00, 0x00, 0x00, 0x00,
                0x00, 0x00, 0x91, 0x8a, 0xa8, 0x74, 0x72, 0x61, 0x63, 0x65, 0x5f, 0x69, 0x64, 0xcf, 0x04, 0xda,
                0x02, 0x0f, 0x60, 0x8a, 0x01, 0x6e, 0xa7, 0x73, 0x70, 0x61, 0x6e, 0x5f, 0x69, 0x64, 0xcf, 0x1f,
                0x86, 0x4e, 0x72, 0xd2, 0xc3, 0xe3, 0x2b, 0xa4, 0x6e, 0x61, 0x6d, 0x65, 0xb3, 0x61, 0x73, 0x70,
                0x6e, 0x65, 0x74, 0x5f, 0x63, 0x6f, 0x72, 0x65, 0x2e, 0x72, 0x65, 0x71, 0x75, 0x65, 0x73, 0x74,
                0xa8, 0x72, 0x65, 0x73, 0x6f, 0x75, 0x72, 0x63, 0x65, 0xae, 0x47, 0x45, 0x54, 0x20, 0x48, 0x6f,
                0x6d, 0x65, 0x2f, 0x49, 0x6e, 0x64, 0x65, 0x78, 0xa7, 0x73, 0x65, 0x72, 0x76, 0x69, 0x63, 0x65,
                0xb7, 0x53, 0x61, 0x6d, 0x70, 0x6c, 0x65, 0x73, 0x2e, 0x41, 0x73, 0x70, 0x4e, 0x65, 0x74, 0x43,
                0x6f, 0x72, 0x65, 0x4d, 0x76, 0x63, 0x33, 0x31, 0xa4, 0x74, 0x79, 0x70, 0x65, 0xa3, 0x77, 0x65,
                0x62, 0xa5, 0x73, 0x74, 0x61, 0x72, 0x74, 0xcf, 0x16, 0x43, 0xc9, 0xd0, 0x3b, 0x31, 0x91, 0xb8,
                0xa8, 0x64, 0x75, 0x72, 0x61, 0x74, 0x69, 0x6f, 0x6e, 0xce, 0x00, 0x18, 0x4d, 0xe0, 0xa4, 0x6d,
                0x65, 0x74, 0x61, 0x89, 0xa3, 0x65, 0x6e, 0x76, 0xb3, 0x63, 0x6f, 0x6c, 0x69, 0x6e, 0x2d, 0x6c,
                0x6f, 0x63, 0x61, 0x6c, 0x2d, 0x6f, 0x72, 0x63, 0x68, 0x61, 0x72, 0x64, 0xa7, 0x76, 0x65, 0x72,
                0x73, 0x69, 0x6f, 0x6e, 0xa5, 0x31, 0x2e, 0x30, 0x2e, 0x30, 0xa9, 0x73, 0x70, 0x61, 0x6e, 0x2e,
                0x6b, 0x69, 0x6e, 0x64, 0xa6, 0x73, 0x65, 0x72, 0x76, 0x65, 0x72, 0xb0, 0x68, 0x74, 0x74, 0x70,
                0x2e, 0x73, 0x74, 0x61, 0x74, 0x75, 0x73, 0x5f, 0x63, 0x6f, 0x64, 0x65, 0xa3, 0x32, 0x30, 0x30,
                0xab, 0x68, 0x74, 0x74, 0x70, 0x2e, 0x6d, 0x65, 0x74, 0x68, 0x6f, 0x64, 0xa3, 0x47, 0x45, 0x54,
                0xb9, 0x68, 0x74, 0x74, 0x70, 0x2e, 0x72, 0x65, 0x71, 0x75, 0x65, 0x73, 0x74, 0x2e, 0x68, 0x65,
                0x61, 0x64, 0x65, 0x72, 0x73, 0x2e, 0x68, 0x6f, 0x73, 0x74, 0xaf, 0x6c, 0x6f, 0x63, 0x61, 0x6c,
                0x68, 0x6f, 0x73, 0x74, 0x3a, 0x35, 0x34, 0x35, 0x36, 0x34, 0xa8, 0x68, 0x74, 0x74, 0x70, 0x2e,
                0x75, 0x72, 0x6c, 0xb7, 0x68, 0x74, 0x74, 0x70, 0x3a, 0x2f, 0x2f, 0x6c, 0x6f, 0x63, 0x61, 0x6c,
                0x68, 0x6f, 0x73, 0x74, 0x3a, 0x35, 0x34, 0x35, 0x36, 0x34, 0x2f, 0xa8, 0x6c, 0x61, 0x6e, 0x67,
                0x75, 0x61, 0x67, 0x65, 0xa6, 0x64, 0x6f, 0x74, 0x6e, 0x65, 0x74, 0xa9, 0x63, 0x6f, 0x6d, 0x70,
                0x6f, 0x6e, 0x65, 0x6e, 0x74, 0xab, 0x61, 0x73, 0x70, 0x6e, 0x65, 0x74, 0x5f, 0x63, 0x6f, 0x72,
                0x65, 0xa7, 0x6d, 0x65, 0x74, 0x72, 0x69, 0x63, 0x73, 0x81, 0xb5, 0x5f, 0x73, 0x61, 0x6d, 0x70,
                0x6c, 0x69, 0x6e, 0x67, 0x5f, 0x70, 0x72, 0x69, 0x6f, 0x72, 0x69, 0x74, 0x79, 0x5f, 0x76, 0x31,
                0xcb, 0x3f, 0xf0, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x91, 0x8a, 0xa8, 0x74, 0x72, 0x61, 0x63,
                0x65, 0x5f, 0x69, 0x64, 0xcf, 0x2b, 0x59, 0x73, 0x0e, 0x0e, 0xb8, 0xab, 0xb0, 0xa7, 0x73, 0x70,
                0x61, 0x6e, 0x5f, 0x69, 0x64, 0xcf, 0x58, 0xb9, 0xc4, 0x7f, 0xd9, 0x73, 0xd8, 0xac, 0xa4, 0x6e,
                0x61, 0x6d, 0x65, 0xb3, 0x61, 0x73, 0x70, 0x6e, 0x65, 0x74, 0x5f, 0x63, 0x6f, 0x72, 0x65, 0x2e,
                0x72, 0x65, 0x71, 0x75, 0x65, 0x73, 0x74, 0xa8, 0x72, 0x65, 0x73, 0x6f, 0x75, 0x72, 0x63, 0x65,
                0xae, 0x47, 0x45, 0x54, 0x20, 0x48, 0x6f, 0x6d, 0x65, 0x2f, 0x49, 0x6e, 0x64, 0x65, 0x78, 0xa7,
                0x73, 0x65, 0x72, 0x76, 0x69, 0x63, 0x65, 0xb7, 0x53, 0x61, 0x6d, 0x70, 0x6c, 0x65, 0x73, 0x2e,
                0x41, 0x73, 0x70, 0x4e, 0x65, 0x74, 0x43, 0x6f, 0x72, 0x65, 0x4d, 0x76, 0x63, 0x33, 0x31, 0xa4,
                0x74, 0x79, 0x70, 0x65, 0xa3, 0x77, 0x65, 0x62, 0xa5, 0x73, 0x74, 0x61, 0x72, 0x74, 0xcf, 0x16,
                0x43, 0xc9, 0xd0, 0x44, 0x9e, 0x8a, 0x08, 0xa8, 0x64, 0x75, 0x72, 0x61, 0x74, 0x69, 0x6f, 0x6e,
                0xce, 0x00, 0x15, 0x55, 0xb8, 0xa4, 0x6d, 0x65, 0x74, 0x61, 0x89, 0xa3, 0x65, 0x6e, 0x76, 0xb3,
                0x63, 0x6f, 0x6c, 0x69, 0x6e, 0x2d, 0x6c, 0x6f, 0x63, 0x61, 0x6c, 0x2d, 0x6f, 0x72, 0x63, 0x68,
                0x61, 0x72, 0x64, 0xa7, 0x76, 0x65, 0x72, 0x73, 0x69, 0x6f, 0x6e, 0xa5, 0x31, 0x2e, 0x30, 0x2e,
                0x30, 0xa9, 0x73, 0x70, 0x61, 0x6e, 0x2e, 0x6b, 0x69, 0x6e, 0x64, 0xa6, 0x73, 0x65, 0x72, 0x76,
                0x65, 0x72, 0xb0, 0x68, 0x74, 0x74, 0x70, 0x2e, 0x73, 0x74, 0x61, 0x74, 0x75, 0x73, 0x5f, 0x63,
                0x6f, 0x64, 0x65, 0xa3, 0x32, 0x30, 0x30, 0xab, 0x68, 0x74, 0x74, 0x70, 0x2e, 0x6d, 0x65, 0x74,
                0x68, 0x6f, 0x64, 0xa3, 0x47, 0x45, 0x54, 0xb9, 0x68, 0x74, 0x74, 0x70, 0x2e, 0x72, 0x65, 0x71,
                0x75, 0x65, 0x73, 0x74, 0x2e, 0x68, 0x65, 0x61, 0x64, 0x65, 0x72, 0x73, 0x2e, 0x68, 0x6f, 0x73,
                0x74, 0xaf, 0x6c, 0x6f, 0x63, 0x61, 0x6c, 0x68, 0x6f, 0x73, 0x74, 0x3a, 0x35, 0x34, 0x35, 0x36,
                0x34, 0xa8, 0x68, 0x74, 0x74, 0x70, 0x2e, 0x75, 0x72, 0x6c, 0xb7, 0x68, 0x74, 0x74, 0x70, 0x3a,
                0x2f, 0x2f, 0x6c, 0x6f, 0x63, 0x61, 0x6c, 0x68, 0x6f, 0x73, 0x74, 0x3a, 0x35, 0x34, 0x35, 0x36,
                0x34, 0x2f, 0xa8, 0x6c, 0x61, 0x6e, 0x67, 0x75, 0x61, 0x67, 0x65, 0xa6, 0x64, 0x6f, 0x74, 0x6e,
                0x65, 0x74, 0xa9, 0x63, 0x6f, 0x6d, 0x70, 0x6f, 0x6e, 0x65, 0x6e, 0x74, 0xab, 0x61, 0x73, 0x70,
                0x6e, 0x65, 0x74, 0x5f, 0x63, 0x6f, 0x72, 0x65, 0xa7, 0x6d, 0x65, 0x74, 0x72, 0x69, 0x63, 0x73,
                0x81, 0xb5, 0x5f, 0x73, 0x61, 0x6d, 0x70, 0x6c, 0x69, 0x6e, 0x67, 0x5f, 0x70, 0x72, 0x69, 0x6f,
                0x72, 0x69, 0x74, 0x79, 0x5f, 0x76, 0x31, 0xcb, 0x3f, 0xf0, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00,
                0x91, 0x8a, 0xa8, 0x74, 0x72, 0x61, 0x63, 0x65, 0x5f, 0x69, 0x64, 0xcf, 0x08, 0x1c, 0x42, 0xd5,
                0x85, 0xff, 0x02, 0xfd, 0xa7, 0x73, 0x70, 0x61, 0x6e, 0x5f, 0x69, 0x64, 0xcf, 0x53, 0xee, 0x1d,
                0x6e, 0xd1, 0xa6, 0x17, 0x63, 0xa4, 0x6e, 0x61, 0x6d, 0x65, 0xb3, 0x61, 0x73, 0x70, 0x6e, 0x65,
                0x74, 0x5f, 0x63, 0x6f, 0x72, 0x65, 0x2e, 0x72, 0x65, 0x71, 0x75, 0x65, 0x73, 0x74, 0xa8, 0x72,
                0x65, 0x73, 0x6f, 0x75, 0x72, 0x63, 0x65, 0xae, 0x47, 0x45, 0x54, 0x20, 0x48, 0x6f, 0x6d, 0x65,
                0x2f, 0x49, 0x6e, 0x64, 0x65, 0x78, 0xa7, 0x73, 0x65, 0x72, 0x76, 0x69, 0x63, 0x65, 0xb7, 0x53,
                0x61, 0x6d, 0x70, 0x6c, 0x65, 0x73, 0x2e, 0x41, 0x73, 0x70, 0x4e, 0x65, 0x74, 0x43, 0x6f, 0x72,
                0x65, 0x4d, 0x76, 0x63, 0x33, 0x31, 0xa4, 0x74, 0x79, 0x70, 0x65, 0xa3, 0x77, 0x65, 0x62, 0xa5,
                0x73, 0x74, 0x61, 0x72, 0x74, 0xcf, 0x16, 0x43, 0xc9, 0xd0, 0x4d, 0xfa, 0xec, 0x40, 0xa8, 0x64,
                0x75, 0x72, 0x61, 0x74, 0x69, 0x6f, 0x6e, 0xce, 0x00, 0x15, 0x70, 0x48, 0xa4, 0x6d, 0x65, 0x74,
                0x61, 0x89, 0xa3, 0x65, 0x6e, 0x76, 0xb3, 0x63, 0x6f, 0x6c, 0x69, 0x6e, 0x2d, 0x6c, 0x6f, 0x63,
                0x61, 0x6c, 0x2d, 0x6f, 0x72, 0x63, 0x68, 0x61, 0x72, 0x64, 0xa7, 0x76, 0x65, 0x72, 0x73, 0x69,
                0x6f, 0x6e, 0xa5, 0x31, 0x2e, 0x30, 0x2e, 0x30, 0xa9, 0x73, 0x70, 0x61, 0x6e, 0x2e, 0x6b, 0x69,
                0x6e, 0x64, 0xa6, 0x73, 0x65, 0x72, 0x76, 0x65, 0x72, 0xb0, 0x68, 0x74, 0x74, 0x70, 0x2e, 0x73,
                0x74, 0x61, 0x74, 0x75, 0x73, 0x5f, 0x63, 0x6f, 0x64, 0x65, 0xa3, 0x32, 0x30, 0x30, 0xab, 0x68,
                0x74, 0x74, 0x70, 0x2e, 0x6d, 0x65, 0x74, 0x68, 0x6f, 0x64, 0xa3, 0x47, 0x45, 0x54, 0xb9, 0x68,
                0x74, 0x74, 0x70, 0x2e, 0x72, 0x65, 0x71, 0x75, 0x65, 0x73, 0x74, 0x2e, 0x68, 0x65, 0x61, 0x64,
                0x65, 0x72, 0x73, 0x2e, 0x68, 0x6f, 0x73, 0x74, 0xaf, 0x6c, 0x6f, 0x63, 0x61, 0x6c, 0x68, 0x6f,
                0x73, 0x74, 0x3a, 0x35, 0x34, 0x35, 0x36, 0x34, 0xa8, 0x68, 0x74, 0x74, 0x70, 0x2e, 0x75, 0x72,
                0x6c, 0xb7, 0x68, 0x74, 0x74, 0x70, 0x3a, 0x2f, 0x2f, 0x6c, 0x6f, 0x63, 0x61, 0x6c, 0x68, 0x6f,
                0x73, 0x74, 0x3a, 0x35, 0x34, 0x35, 0x36, 0x34, 0x2f, 0xa8, 0x6c, 0x61, 0x6e, 0x67, 0x75, 0x61,
                0x67, 0x65, 0xa6, 0x64, 0x6f, 0x74, 0x6e, 0x65, 0x74, 0xa9, 0x63, 0x6f, 0x6d, 0x70, 0x6f, 0x6e,
                0x65, 0x6e, 0x74, 0xab, 0x61, 0x73, 0x70, 0x6e, 0x65, 0x74, 0x5f, 0x63, 0x6f, 0x72, 0x65, 0xa7,
                0x6d, 0x65, 0x74, 0x72, 0x69, 0x63, 0x73, 0x81, 0xb5, 0x5f, 0x73, 0x61, 0x6d, 0x70, 0x6c, 0x69,
                0x6e, 0x67, 0x5f, 0x70, 0x72, 0x69, 0x6f, 0x72, 0x69, 0x74, 0x79, 0x5f, 0x76, 0x31, 0xcb, 0x3f,
                0xf0, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x0d, 0x0a, 0x30, 0x0d, 0x0a, 0x0d, 0x0a
            };

            return example;
        }
    }
}