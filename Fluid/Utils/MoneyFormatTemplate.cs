using System.Globalization;
using System.Text;

namespace Fluid.Utils
{
    /// <summary>
    /// The amount placeholders supported by a money format string. They are intentionally
    /// culture independent, as defined by https://help.shopify.com/en/manual/payments/currency-formatting.
    /// </summary>
    internal enum MoneyAmountFormat
    {
        Amount = 0,
        AmountNoDecimals = 1,
        AmountWithCommaSeparator = 2,
        AmountNoDecimalsWithCommaSeparator = 3,
        AmountWithApostropheSeparator = 4,
        AmountNoDecimalsWithSpaceSeparator = 5,
        AmountWithSpaceSeparator = 6,
        AmountWithPeriodAndSpaceSeparator = 7,
    }

    internal enum MoneySegmentKind
    {
        Literal,
        Amount,
        CurrencyCode,
        CurrencySymbol,
    }

    internal readonly struct MoneyFormatSegment
    {
        public MoneyFormatSegment(string literal)
        {
            Kind = MoneySegmentKind.Literal;
            Literal = literal;
            Amount = default;
        }

        public MoneyFormatSegment(MoneyAmountFormat amount)
        {
            Kind = MoneySegmentKind.Amount;
            Literal = null;
            Amount = amount;
        }

        public MoneyFormatSegment(MoneySegmentKind kind)
        {
            Kind = kind;
            Literal = null;
            Amount = default;
        }

        public MoneySegmentKind Kind { get; }
        public string Literal { get; }
        public MoneyAmountFormat Amount { get; }
    }

    /// <summary>
    /// A pre-parsed money format string, e.g. <c>${{amount}}</c>. Parsing happens once, when the
    /// format is assigned on <see cref="MoneyOptions"/>, so that rendering never rescans the string.
    /// </summary>
    internal sealed class MoneyFormatTemplate
    {
        // Indexed by (int)MoneyAmountFormat * 2 + (noDecimals ? 1 : 0)
        private static readonly NumberFormatInfo[] AmountFormats = CreateAmountFormats();

        private readonly MoneyFormatSegment[] _segments;

        private MoneyFormatTemplate(MoneyFormatSegment[] segments)
        {
            _segments = segments;
        }

        public static MoneyFormatTemplate Parse(string format)
        {
            if (format == null)
            {
                return null;
            }

            var segments = new List<MoneyFormatSegment>();

            // The start of the literal text that hasn't been added to the segments yet.
            var literalStart = 0;
            var searchIndex = 0;

            while (searchIndex < format.Length)
            {
                var start = format.IndexOf("{{", searchIndex, StringComparison.Ordinal);

                if (start == -1)
                {
                    break;
                }

                var end = format.IndexOf("}}", start + 2, StringComparison.Ordinal);

                if (end == -1)
                {
                    break;
                }

                var name = format.Substring(start + 2, end - start - 2).Trim();

                if (!TryCreateSegment(name, out var segment))
                {
                    // Unknown placeholders are rendered verbatim, they stay part of the current literal.
                    searchIndex = start + 2;
                    continue;
                }

                if (start > literalStart)
                {
                    segments.Add(new MoneyFormatSegment(format.Substring(literalStart, start - literalStart)));
                }

                segments.Add(segment);
                literalStart = end + 2;
                searchIndex = literalStart;
            }

            if (literalStart < format.Length)
            {
                segments.Add(new MoneyFormatSegment(format.Substring(literalStart)));
            }

            return new MoneyFormatTemplate(segments.ToArray());
        }

        private static bool TryCreateSegment(string name, out MoneyFormatSegment segment)
        {
            switch (name)
            {
                case "amount": segment = new MoneyFormatSegment(MoneyAmountFormat.Amount); return true;
                case "amount_no_decimals": segment = new MoneyFormatSegment(MoneyAmountFormat.AmountNoDecimals); return true;
                case "amount_with_comma_separator": segment = new MoneyFormatSegment(MoneyAmountFormat.AmountWithCommaSeparator); return true;
                case "amount_no_decimals_with_comma_separator": segment = new MoneyFormatSegment(MoneyAmountFormat.AmountNoDecimalsWithCommaSeparator); return true;
                case "amount_with_apostrophe_separator": segment = new MoneyFormatSegment(MoneyAmountFormat.AmountWithApostropheSeparator); return true;
                case "amount_no_decimals_with_space_separator": segment = new MoneyFormatSegment(MoneyAmountFormat.AmountNoDecimalsWithSpaceSeparator); return true;
                case "amount_with_space_separator": segment = new MoneyFormatSegment(MoneyAmountFormat.AmountWithSpaceSeparator); return true;
                case "amount_with_period_and_space_separator": segment = new MoneyFormatSegment(MoneyAmountFormat.AmountWithPeriodAndSpaceSeparator); return true;

                // Fluid specific placeholders, used to support a currency that is resolved at rendering time.
                case "currency": segment = new MoneyFormatSegment(MoneySegmentKind.CurrencyCode); return true;
                case "currency_symbol": segment = new MoneyFormatSegment(MoneySegmentKind.CurrencySymbol); return true;

                default: segment = default; return false;
            }
        }

        /// <summary>
        /// Renders the template.
        /// </summary>
        /// <param name="builder">The builder to render to.</param>
        /// <param name="amount">The amount to render.</param>
        /// <param name="currency">The currency used by the <c>currency</c> and <c>currency_symbol</c> placeholders.</param>
        /// <param name="noDecimals">When <c>true</c> every amount placeholder is rendered without decimals.</param>
        public void Render(ref ValueStringBuilder builder, decimal amount, MoneyCurrency currency, bool noDecimals)
        {
            foreach (var segment in _segments)
            {
                switch (segment.Kind)
                {
                    case MoneySegmentKind.Literal:
                        builder.Append(segment.Literal);
                        break;

                    case MoneySegmentKind.Amount:
                        AppendAmount(ref builder, amount, segment.Amount, noDecimals);
                        break;

                    case MoneySegmentKind.CurrencyCode:
                        builder.Append(currency.Code);
                        break;

                    case MoneySegmentKind.CurrencySymbol:
                        builder.Append(currency.Symbol);
                        break;
                }
            }
        }

        private static void AppendAmount(ref ValueStringBuilder builder, decimal amount, MoneyAmountFormat format, bool noDecimals)
        {
            var numberFormat = AmountFormats[((int)format * 2) + (noDecimals ? 1 : 0)];
            var rounded = Math.Round(amount, numberFormat.NumberDecimalDigits, MidpointRounding.AwayFromZero);

            builder.Append(rounded.ToString("N", numberFormat));
        }

        private static NumberFormatInfo[] CreateAmountFormats()
        {
            // group separator, decimal separator, decimal digits
            (string Group, string Decimal, int Digits)[] specifications =
            [
                (",", ".", 2), // amount
                (",", ".", 0), // amount_no_decimals
                (".", ",", 2), // amount_with_comma_separator
                (".", ",", 0), // amount_no_decimals_with_comma_separator
                ("'", ".", 2), // amount_with_apostrophe_separator
                (" ", ".", 0), // amount_no_decimals_with_space_separator
                (" ", ",", 2), // amount_with_space_separator
                (" ", ".", 2), // amount_with_period_and_space_separator
            ];

            var result = new NumberFormatInfo[specifications.Length * 2];

            for (var i = 0; i < specifications.Length; i++)
            {
                var specification = specifications[i];

                result[i * 2] = CreateAmountFormat(specification.Group, specification.Decimal, specification.Digits);
                result[(i * 2) + 1] = CreateAmountFormat(specification.Group, specification.Decimal, 0);
            }

            return result;
        }

        private static NumberFormatInfo CreateAmountFormat(string groupSeparator, string decimalSeparator, int decimalDigits)
        {
            var numberFormat = (NumberFormatInfo)CultureInfo.InvariantCulture.NumberFormat.Clone();

            numberFormat.NumberGroupSeparator = groupSeparator;
            numberFormat.NumberDecimalSeparator = decimalSeparator;
            numberFormat.NumberDecimalDigits = decimalDigits;
            numberFormat.NumberGroupSizes = [3];

            return NumberFormatInfo.ReadOnly(numberFormat);
        }
    }
}
