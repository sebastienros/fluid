using Fluid.Utils;
using System.Collections.Concurrent;
using System.Globalization;

namespace Fluid
{
    /// <summary>
    /// Configures the money filters registered by <see cref="Filters.MoneyFilters.WithMoneyFilters"/>.
    /// </summary>
    /// <remarks>
    /// Like <see cref="TemplateOptions"/> an instance is expected to be configured once, before any
    /// template is rendered, and then shared. Formats and currencies are cached the first time they are used.
    /// </remarks>
    public class MoneyOptions
    {
        private string _moneyFormat;
        private string _moneyWithCurrencyFormat;

        private readonly ConcurrentDictionary<string, NumberFormatInfo> _numberFormats = new(StringComparer.Ordinal);

        /// <summary>
        /// Gets or sets whether amounts are expressed in cents, like in Shopify where prices are stored as integers.
        /// When <c>true</c>, the input of the money filters is divided by 100. The default is <c>false</c>.
        /// </summary>
        /// <example>
        /// With <c>AmountsInCents</c> set to <c>true</c>, <c>{{ 1450 | money }}</c> renders <c>$14.50</c>.
        /// </example>
        public bool AmountsInCents { get; set; }

        /// <summary>
        /// Gets or sets the ISO 4217 code of the currency to use, e.g. <c>EUR</c>.
        /// When <c>null</c>, the currency of <see cref="TemplateOptions.CultureInfo"/> is used, falling back to <c>USD</c>.
        /// </summary>
        public string Currency { get; set; }

        /// <summary>
        /// Gets or sets the format used by the <c>money</c> and <c>money_without_trailing_zeros</c> filters.
        /// When <c>null</c>, the format is derived from <see cref="TemplateOptions.CultureInfo"/>.
        /// </summary>
        /// <remarks>
        /// The supported placeholders are <c>{{amount}}</c>, <c>{{amount_no_decimals}}</c>,
        /// <c>{{amount_with_comma_separator}}</c>, <c>{{amount_no_decimals_with_comma_separator}}</c>,
        /// <c>{{amount_with_apostrophe_separator}}</c>, <c>{{amount_no_decimals_with_space_separator}}</c>,
        /// <c>{{amount_with_space_separator}}</c>, <c>{{amount_with_period_and_space_separator}}</c>,
        /// and the Fluid specific <c>{{currency}}</c> and <c>{{currency_symbol}}</c>.
        /// Unknown placeholders are rendered verbatim.
        /// </remarks>
        /// <example><c>"${{amount}}"</c></example>
        public string MoneyFormat
        {
            get => _moneyFormat;
            set
            {
                _moneyFormat = value;
                ParsedMoneyFormat = MoneyFormatTemplate.Parse(value);
            }
        }

        /// <summary>
        /// Gets or sets the format used by the <c>money_with_currency</c> filter. When <c>null</c>, the
        /// format is derived from <see cref="MoneyFormat"/> when it is set, or from <see cref="TemplateOptions.CultureInfo"/>.
        /// </summary>
        /// <remarks>
        /// Supports the same placeholders as <see cref="MoneyFormat"/>.
        /// </remarks>
        /// <example><c>"${{amount}} {{currency}}"</c></example>
        public string MoneyWithCurrencyFormat
        {
            get => _moneyWithCurrencyFormat;
            set
            {
                _moneyWithCurrencyFormat = value;
                ParsedMoneyWithCurrencyFormat = MoneyFormatTemplate.Parse(value);
            }
        }

        /// <summary>
        /// Gets the currencies known to the money filters, keyed by their ISO 4217 code.
        /// Entries can be added or replaced to support more currencies, or to use a different symbol.
        /// A currency that is not in this collection is rendered using its code as the symbol.
        /// </summary>
        public IDictionary<string, MoneyCurrency> Currencies { get; } = CreateDefaultCurrencies();

        internal MoneyFormatTemplate ParsedMoneyFormat { get; private set; }

        internal MoneyFormatTemplate ParsedMoneyWithCurrencyFormat { get; private set; }

        internal NumberFormatInfo GetNumberFormat(CultureInfo culture, MoneyCurrency currency)
        {
            var key = culture.Name + "|" + currency.Code;

            if (_numberFormats.TryGetValue(key, out var numberFormat))
            {
                return numberFormat;
            }

            numberFormat = (NumberFormatInfo)culture.NumberFormat.Clone();

            numberFormat.CurrencySymbol = currency.Symbol;
            numberFormat.CurrencyDecimalDigits = currency.DecimalDigits;

            // Prices are rendered with a minus sign instead of the accounting notation some cultures use,
            // e.g. -$10.00 instead of ($10.00). The negative pattern is aligned on the positive one so the
            // symbol stays at the same place. See https://learn.microsoft.com/dotnet/api/system.globalization.numberformatinfo.currencynegativepattern
            numberFormat.CurrencyNegativePattern = numberFormat.CurrencyPositivePattern switch
            {
                1 => 5, // n$ -> -n$
                2 => 9, // $ n -> -$ n
                3 => 8, // n $ -> -n $
                _ => 1, // $n -> -$n
            };

            numberFormat.NumberNegativePattern = 1; // -n

            // The 'N' format is used to render amounts without their currency symbol. Align it on the
            // currency formatting of the culture so both filters produce consistent separators.
            numberFormat.NumberGroupSeparator = numberFormat.CurrencyGroupSeparator;
            numberFormat.NumberDecimalSeparator = numberFormat.CurrencyDecimalSeparator;
            numberFormat.NumberGroupSizes = numberFormat.CurrencyGroupSizes;
            numberFormat.NumberDecimalDigits = currency.DecimalDigits;

            numberFormat = NumberFormatInfo.ReadOnly(numberFormat);

            _numberFormats[key] = numberFormat;

            return numberFormat;
        }

        private static Dictionary<string, MoneyCurrency> CreateDefaultCurrencies()
        {
            MoneyCurrency[] currencies =
            [
                new("AED", "د.إ"),
                new("ARS", "$"),
                new("AUD", "$"),
                new("BGN", "лв"),
                new("BRL", "R$"),
                new("CAD", "$"),
                new("CHF", "CHF"),
                new("CLP", "$", 0),
                new("CNY", "¥"),
                new("COP", "$"),
                new("CZK", "Kč"),
                new("DKK", "kr"),
                new("EGP", "£"),
                new("EUR", "€"),
                new("GBP", "£"),
                new("HKD", "$"),
                new("HUF", "Ft"),
                new("IDR", "Rp"),
                new("ILS", "₪"),
                new("INR", "₹"),
                new("ISK", "kr", 0),
                new("JPY", "¥", 0),
                new("KRW", "₩", 0),
                new("KWD", "د.ك", 3),
                new("MAD", "د.م."),
                new("MXN", "$"),
                new("MYR", "RM"),
                new("NGN", "₦"),
                new("NOK", "kr"),
                new("NZD", "$"),
                new("PEN", "S/"),
                new("PHP", "₱"),
                new("PKR", "₨"),
                new("PLN", "zł"),
                new("RON", "lei"),
                new("RUB", "₽"),
                new("SAR", "﷼"),
                new("SEK", "kr"),
                new("SGD", "$"),
                new("THB", "฿"),
                new("TRY", "₺"),
                new("TWD", "$"),
                new("UAH", "₴"),
                new("USD", "$"),
                new("VND", "₫", 0),
                new("ZAR", "R"),
            ];

            var result = new Dictionary<string, MoneyCurrency>(currencies.Length, StringComparer.OrdinalIgnoreCase);

            foreach (var currency in currencies)
            {
                result[currency.Code] = currency;
            }

            return result;
        }
    }
}
