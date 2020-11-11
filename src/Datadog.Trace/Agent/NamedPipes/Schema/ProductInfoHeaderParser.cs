//------------------------------------------------------------------------------
// <auto-generated />
// This file was automatically generated by the UpdateVendors tool.
//------------------------------------------------------------------------------

namespace Datadog.Trace.Agent.NamedPipes
{
    internal class ProductInfoHeaderParser : HttpHeaderParser
    {
        // Unlike most other headers, User-Agent and Server use whitespace as separators
        private const string separator = " ";

        internal static readonly ProductInfoHeaderParser SingleValueParser = new ProductInfoHeaderParser(false);
        internal static readonly ProductInfoHeaderParser MultipleValueParser = new ProductInfoHeaderParser(true);

        private ProductInfoHeaderParser(bool supportsMultipleValues)
            : base(supportsMultipleValues, separator)
        {
        }

        public override bool TryParseValue(string value, object storeValue, ref int index, out object parsedValue)
        {
            parsedValue = null;

            if (string.IsNullOrEmpty(value) || (index == value.Length))
            {
                return false;
            }

            // Skip leading whitespace
            int current = index + HttpRuleParser.GetWhitespaceLength(value, index);

            if (current == value.Length)
            {
                return false; // whitespace-only values are not valid
            }

            int length = ProductInfoHeaderValue.GetProductInfoLength(value, current, out ProductInfoHeaderValue result);

            if (length == 0)
            {
                return false;
            }

            // GetProductInfoLength() already skipped trailing whitespace. No need to do it here again.
            current = current + length;

            // If we have more values, make sure we saw a whitespace before. Values like "product/1.0(comment)" are
            // invalid since there must be a whitespace between the product and the comment value.
            if (current < value.Length)
            {
                // Note that for \r\n to be a valid whitespace, it must be followed by a space/tab. I.e. it's enough if
                // we check whether the char before the next value is space/tab.
                char lastSeparatorChar = value[current - 1];
                if ((lastSeparatorChar != ' ') && (lastSeparatorChar != '\t'))
                {
                    return false;
                }
            }

            // Separators for "User-Agent" and "Server" headers are whitespace. This is different from most other headers
            // where comma/semicolon is used as separator.
            index = current;
            parsedValue = result!;
            return true;
        }
    }
}