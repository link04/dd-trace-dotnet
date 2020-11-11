//------------------------------------------------------------------------------
// <auto-generated />
// This file was automatically generated by the UpdateVendors tool.
//------------------------------------------------------------------------------

using System;
using System.Buffers;
using System.Text;
using System.Text.Unicode;
using Datadog.Trace.Vendors.MessagePack.Internal;

namespace Datadog.Trace.Agent.NamedPipes
{
    internal readonly struct HeaderDescriptor : IEquatable<HeaderDescriptor>
    {
        private readonly string _headerName;
        private readonly KnownHeader _knownHeader;

        public HeaderDescriptor(KnownHeader knownHeader)
        {
            _knownHeader = knownHeader;
            _headerName = knownHeader.Name;
        }

        // This should not be used directly; use static TryGet below
        internal HeaderDescriptor(string headerName)
        {
            _headerName = headerName;
            _knownHeader = null;
        }

        public string Name => _headerName;

        public HttpHeaderParser Parser => _knownHeader?.Parser;

        public HttpHeaderType HeaderType => _knownHeader == null ? HttpHeaderType.Custom : _knownHeader.HeaderType;

        public KnownHeader KnownHeader => _knownHeader;

        public bool Equals(HeaderDescriptor other) =>
            _knownHeader == null ?
                string.Equals(_headerName, other._headerName, StringComparison.OrdinalIgnoreCase) :
                _knownHeader == other._knownHeader;

        public override int GetHashCode() => _knownHeader?.GetHashCode() ?? StringComparer.OrdinalIgnoreCase.GetHashCode(_headerName);

        public override bool Equals(object obj) => throw new InvalidOperationException();   // Ensure this is never called, to avoid boxing

        // Returns false for invalid header name.
        public static bool TryGet(string headerName, out HeaderDescriptor descriptor)
        {
            // Debug.Assert(!string.IsNullOrEmpty(headerName));

            KnownHeader knownHeader = KnownHeaders.TryGetKnownHeader(headerName);
            if (knownHeader != null)
            {
                descriptor = new HeaderDescriptor(knownHeader);
                return true;
            }

            if (!HttpRuleParser.IsToken(headerName))
            {
                descriptor = default(HeaderDescriptor);
                return false;
            }

            descriptor = new HeaderDescriptor(headerName);
            return true;
        }

        // Returns false for invalid header name.
        public static bool TryGet(byte[] headerName, out HeaderDescriptor descriptor)
        {
            // Debug.Assert(headerName.Length > 0);

            KnownHeader knownHeader = KnownHeaders.TryGetKnownHeader(headerName);
            if (knownHeader != null)
            {
                descriptor = new HeaderDescriptor(knownHeader);
                return true;
            }

            if (!HttpRuleParser.IsToken(headerName))
            {
                descriptor = default(HeaderDescriptor);
                return false;
            }

            descriptor = new HeaderDescriptor(HttpRuleParser.GetTokenString(headerName));
            return true;
        }

        internal static bool TryGetStaticQPackHeader(int index, out HeaderDescriptor descriptor, out string knownValue)
        {
            // Debug.Assert(index >= 0);

            // Micro-opt: store field to variable to prevent Length re-read and use unsigned to avoid bounds check.
            Tuple<HeaderDescriptor, string>[] qpackStaticTable = QPackStaticTable.HeaderLookup;
            // Debug.Assert(qpackStaticTable.Length == 99);

            uint uindex = (uint)index;

            if (uindex < (uint)qpackStaticTable.Length)
            {
                (descriptor, knownValue) = qpackStaticTable[uindex];
                return true;
            }
            else
            {
                descriptor = default;
                knownValue = null;
                return false;
            }
        }

        public HeaderDescriptor AsCustomHeader()
        {
            // Debug.Assert(_knownHeader != null);
            // Debug.Assert(_knownHeader.HeaderType != HttpHeaderType.Custom);
            return new HeaderDescriptor(_knownHeader.Name);
        }

        public string GetHeaderValue(byte[] headerValue, Encoding valueEncoding)
        {
            if (headerValue.Length == 0)
            {
                return string.Empty;
            }

            // If it's a known header value, use the known value instead of allocating a new string.
            if (_knownHeader != null)
            {
                string[] knownValues = _knownHeader.KnownValues;
                if (knownValues != null)
                {
                    for (int i = 0; i < knownValues.Length; i++)
                    {
                        if (ByteArrayHelpers.EqualsOrdinalAsciiIgnoreCase(knownValues[i], headerValue))
                        {
                            return knownValues[i];
                        }
                    }
                }

                if (_knownHeader == KnownHeaders.ContentType)
                {
                    string contentType = GetKnownContentType(headerValue);
                    if (contentType != null)
                    {
                        return contentType;
                    }
                }
                else if (_knownHeader == KnownHeaders.Location)
                {
                    // Normally Location should be in ISO-8859-1 but occasionally some servers respond with UTF-8.
                    if (TryDecodeUtf8(headerValue, out string decoded))
                    {
                        return decoded;
                    }
                }
            }

            return (valueEncoding ?? HttpRuleParser.DefaultHttpEncoding).GetString(headerValue);
        }

        internal static string GetKnownContentType(byte[] contentTypeValue)
        {
            string candidate = null;
            switch (contentTypeValue.Length)
            {
                case 8:
                    switch (contentTypeValue[7] | 0x20)
                    {
                        case 'l': candidate = "text/xml"; break; // text/xm[l]
                        case 's': candidate = "text/css"; break; // text/cs[s]
                        case 'v': candidate = "text/csv"; break; // text/cs[v]
                    }
                    break;

                case 9:
                    switch (contentTypeValue[6] | 0x20)
                    {
                        case 'g': candidate = "image/gif"; break; // image/[g]if
                        case 'p': candidate = "image/png"; break; // image/[p]ng
                        case 't': candidate = "text/html"; break; // text/h[t]ml
                    }
                    break;

                case 10:
                    switch (contentTypeValue[0] | 0x20)
                    {
                        case 't': candidate = "text/plain"; break; // [t]ext/plain
                        case 'i': candidate = "image/jpeg"; break; // [i]mage/jpeg
                    }
                    break;

                case 15:
                    switch (contentTypeValue[12] | 0x20)
                    {
                        case 'p': candidate = "application/pdf"; break; // application/[p]df
                        case 'x': candidate = "application/xml"; break; // application/[x]ml
                        case 'z': candidate = "application/zip"; break; // application/[z]ip
                    }
                    break;

                case 16:
                    switch (contentTypeValue[12] | 0x20)
                    {
                        case 'g': candidate = "application/grpc"; break; // application/[g]rpc
                        case 'j': candidate = "application/json"; break; // application/[j]son
                    }
                    break;

                case 19:
                    candidate = "multipart/form-data"; // multipart/form-data
                    break;

                case 22:
                    candidate = "application/javascript"; // application/javascript
                    break;

                case 24:
                    switch (contentTypeValue[0] | 0x20)
                    {
                        case 'a': candidate = "application/octet-stream"; break; // application/octet-stream
                        case 't': candidate = "text/html; charset=utf-8"; break; // text/html; charset=utf-8
                    }
                    break;

                case 25:
                    candidate = "text/plain; charset=utf-8"; // text/plain; charset=utf-8
                    break;

                case 31:
                    candidate = "application/json; charset=utf-8"; // application/json; charset=utf-8
                    break;

                case 33:
                    candidate = "application/x-www-form-urlencoded"; // application/x-www-form-urlencoded
                    break;
            }

            // Debug.Assert(candidate is null || candidate.Length == contentTypeValue.Length);

            return candidate != null && ByteArrayHelpers.EqualsOrdinalAsciiIgnoreCase(candidate, contentTypeValue) ?
                       candidate :
                       null;
        }

        private static bool TryDecodeUtf8(byte[] input, out string decoded)
        {
            // char[] rented = ArrayPool<char>.Shared.Rent(input.Length);
            var rented = new char[input.Length];
            try
            {
                if (Utf8.ToUtf16(input, rented, out _, out int charsWritten, replaceInvalidSequences: false) == OperationStatus.Done)
                {
                    decoded = new string(rented, 0, charsWritten);
                    return true;
                }
            }
            finally
            {
                // ArrayPool<char>.Shared.Return(rented);
            }

            decoded = null;
            return false;
        }
    }
}