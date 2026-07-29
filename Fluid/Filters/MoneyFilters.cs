using Fluid.Utils;
using Fluid.Values;
using System.Collections.Concurrent;
using System.Globalization;
using System.Text;

namespace Fluid.Filters
{
    /// <summary>
    /// Implements the Shopify money filters. They are not registered by default, use
    /// <see cref="WithMoneyFilters"/> to add them to a <see cref="FilterCollection"/>.
    /// </summary>
    public static class MoneyFilters
    {
        private const string FallbackCurrency = "USD";

        private static readonly ConcurrentDictionary<string, string> _cultureCurrencies = new(StringComparer.Ordinal);

        private enum MoneyStyle
        {
            Money,
            WithCurrency,
            WithoutCurrency,
            WithoutTrailingZeros,
        }

        /// <summary>
        /// Registers the <c>money</c>, <c>money_with_currency</c>, <c>money_without_currency</c>
        /// and <c>money_without_trailing_zeros</c> filters.
        /// </summary>
        public static FilterCollection WithMoneyFilters(this FilterCollection filters)
        {
            if (filters is null)
            {
                ExceptionHelper.ThrowArgumentNullException(nameof(filters));
            }

            filters.AddFilter("money", Money);
            filters.AddFilter("money_with_currency", MoneyWithCurrency);
            filters.AddFilter("money_without_currency", MoneyWithoutCurrency);
            filters.AddFilter("money_without_trailing_zeros", MoneyWithoutTrailingZeros);

            return filters;
        }

        /// <summary>
        /// Formats an amount with its currency symbol, e.g. <c>$10.00</c>.
        /// </summary>
        public static ValueTask<FluidValue> Money(FluidValue input, FilterArguments arguments, TemplateContext context)
        {
            return Format(input, arguments, context, "money", MoneyStyle.Money);
        }

        /// <summary>
        /// Formats an amount with its currency symbol and its currency code, e.g. <c>$10.00 USD</c>.
        /// </summary>
        public static ValueTask<FluidValue> MoneyWithCurrency(FluidValue input, FilterArguments arguments, TemplateContext context)
        {
            return Format(input, arguments, context, "money_with_currency", MoneyStyle.WithCurrency);
        }

        /// <summary>
        /// Formats an amount without its currency symbol, e.g. <c>10.00</c>.
        /// </summary>
        public static ValueTask<FluidValue> MoneyWithoutCurrency(FluidValue input, FilterArguments arguments, TemplateContext context)
        {
            return Format(input, arguments, context, "money_without_currency", MoneyStyle.WithoutCurrency);
        }

        /// <summary>
        /// Formats an amount with its currency symbol, omitting the decimal separator and the
        /// decimal digits when they are all zeros, e.g. <c>$10</c>.
        /// </summary>
        public static ValueTask<FluidValue> MoneyWithoutTrailingZeros(FluidValue input, FilterArguments arguments, TemplateContext context)
        {
            return Format(input, arguments, context, "money_without_trailing_zeros", MoneyStyle.WithoutTrailingZeros);
        }

        private static StringValue Format(FluidValue input, FilterArguments arguments, TemplateContext context, string filterName, MoneyStyle style)
        {
            if (arguments.Count > 1)
            {
                throw new FluidException($"Filter '{filterName}' expects at most one argument.");
            }

            if (input == null || input.IsNil() || input.Type == FluidValues.Blank || input.Type == FluidValues.Empty)
            {
                return StringValue.Empty;
            }

            var options = context.MoneyOptions ?? TemplateOptions.Default.MoneyOptions;
            var culture = context.CultureInfo ?? CultureInfo.InvariantCulture;

            var amount = input.ToNumberValue();

            if (options.AmountsInCents)
            {
                amount /= 100M;
            }

            var currency = ResolveCurrency(arguments, options, culture);
            var numberFormat = options.GetNumberFormat(culture, currency);

            switch (style)
            {
                case MoneyStyle.WithoutCurrency:
                    // The amount is rendered on its own, the configured formats always contain a currency symbol.
                    return new StringValue(Round(amount, currency.DecimalDigits).ToString("N", numberFormat));

                case MoneyStyle.WithoutTrailingZeros:
                    var rounded = Round(amount, currency.DecimalDigits);
                    var noDecimals = rounded % 1M == 0M;
                    return new StringValue(FormatAmount(rounded, currency, numberFormat, options.ParsedMoneyFormat, noDecimals));

                case MoneyStyle.WithCurrency:
                    return new StringValue(FormatWithCurrency(amount, currency, numberFormat, options));

                default:
                    return new StringValue(FormatAmount(amount, currency, numberFormat, options.ParsedMoneyFormat, noDecimals: false));
            }
        }

        private static string FormatWithCurrency(decimal amount, MoneyCurrency currency, NumberFormatInfo numberFormat, MoneyOptions options)
        {
            var template = options.ParsedMoneyWithCurrencyFormat;

            if (template != null)
            {
                return Render(template, amount, currency, noDecimals: false);
            }

            // No dedicated format: append the currency code to the regular money format.
            var money = FormatAmount(amount, currency, numberFormat, options.ParsedMoneyFormat, noDecimals: false);

            var builder = new ValueStringBuilder(stackalloc char[64]);
            builder.Append(money);
            builder.Append(' ');
            builder.Append(currency.Code);

            return builder.ToString();
        }

        private static string FormatAmount(decimal amount, MoneyCurrency currency, NumberFormatInfo numberFormat, MoneyFormatTemplate template, bool noDecimals)
        {
            if (template != null)
            {
                return Render(template, amount, currency, noDecimals);
            }

            var digits = noDecimals ? 0 : currency.DecimalDigits;

            return Round(amount, digits).ToString(noDecimals ? "C0" : "C", numberFormat);
        }

        private static string Render(MoneyFormatTemplate template, decimal amount, MoneyCurrency currency, bool noDecimals)
        {
            var builder = new ValueStringBuilder(stackalloc char[64]);
            template.Render(ref builder, amount, currency, noDecimals);

            return builder.ToString();
        }

        private static decimal Round(decimal amount, int digits)
        {
            // Shopify rounds half up, whereas .NET rounds half to even by default.
            return Math.Round(amount, digits, MidpointRounding.AwayFromZero);
        }

        private static MoneyCurrency ResolveCurrency(FilterArguments arguments, MoneyOptions options, CultureInfo culture)
        {
            var argument = arguments["currency"];

            if (argument.IsNil())
            {
                argument = arguments.At(0);
            }

            var code = argument.IsNil() ? null : argument.ToStringValue();

            if (string.IsNullOrEmpty(code))
            {
                code = options.Currency;
            }

            if (string.IsNullOrEmpty(code))
            {
                code = GetCultureCurrency(culture);
            }

            if (options.Currencies.TryGetValue(code, out var currency))
            {
                return currency;
            }

            // An unknown currency is rendered with its code as the symbol.
            return new MoneyCurrency(code, code, culture.NumberFormat.CurrencyDecimalDigits);
        }

        private static string GetCultureCurrency(CultureInfo culture)
        {
            var name = culture.Name;

            if (string.IsNullOrEmpty(name))
            {
                // The invariant culture has no region, and its currency symbol is the generic '¤' sign.
                return FallbackCurrency;
            }

            if (_cultureCurrencies.TryGetValue(name, out var code))
            {
                return code;
            }

            code = FindCultureCurrency(culture);
            _cultureCurrencies[name] = code;

            return code;
        }

        private static string FindCultureCurrency(CultureInfo culture)
        {
            try
            {
                return new RegionInfo(culture.Name).ISOCurrencySymbol;
            }
            catch (ArgumentException)
            {
                // Neutral cultures like 'de' have no region, resolve the specific culture first.
            }

            try
            {
                return new RegionInfo(CultureInfo.CreateSpecificCulture(culture.Name).Name).ISOCurrencySymbol;
            }
            catch (ArgumentException)
            {
                // CultureNotFoundException derives from ArgumentException.
                return FallbackCurrency;
            }
        }
    }
}
