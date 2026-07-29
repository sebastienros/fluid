using Fluid.Filters;
using Fluid.Values;
using System;
using System.Globalization;
using Xunit;

namespace Fluid.Tests
{
    public class MoneyFiltersTests
    {
        private static readonly FluidParser _parser = new FluidParser();

        [Theory]
        [InlineData(10, "$10.00")]
        [InlineData(0, "$0.00")]
        [InlineData(1134.65, "$1,134.65")]
        [InlineData(1234567.891, "$1,234,567.89")]
        [InlineData(10.005, "$10.01")] // rounded away from zero, not to even
        [InlineData(-10, "-$10.00")]
        public void Money(decimal value, string expected)
        {
            Assert.Equal(expected, Invoke(MoneyFilters.Money, value));
        }

        [Theory]
        [InlineData(10, "$10.00 USD")]
        [InlineData(1134.65, "$1,134.65 USD")]
        public void MoneyWithCurrency(decimal value, string expected)
        {
            Assert.Equal(expected, Invoke(MoneyFilters.MoneyWithCurrency, value));
        }

        [Theory]
        [InlineData(10, "10.00")]
        [InlineData(1134.65, "1,134.65")]
        [InlineData(-1134.65, "-1,134.65")]
        public void MoneyWithoutCurrency(decimal value, string expected)
        {
            Assert.Equal(expected, Invoke(MoneyFilters.MoneyWithoutCurrency, value));
        }

        [Theory]
        [InlineData(10, "$10")]
        [InlineData(10.5, "$10.50")]
        [InlineData(1134, "$1,134")]
        [InlineData(1134.65, "$1,134.65")]
        [InlineData(0, "$0")]
        public void MoneyWithoutTrailingZeros(decimal value, string expected)
        {
            Assert.Equal(expected, Invoke(MoneyFilters.MoneyWithoutTrailingZeros, value));
        }

        [Theory]
        [InlineData("31.3")]
        [InlineData("31.30")]
        [InlineData("31.300")]
        public void MoneyShouldNotDependOnTheScaleOfTheInput(string value)
        {
            // https://github.com/sebastienros/fluid/issues/238#issuecomment-2360412081
            var amount = decimal.Parse(value, CultureInfo.InvariantCulture);

            Assert.Equal("$31.30", Invoke(MoneyFilters.Money, amount));
            Assert.Equal("31.30", Invoke(MoneyFilters.MoneyWithoutCurrency, amount));
        }

        [Theory]
        [InlineData(1450, "$14.50")]
        [InlineData(1000, "$10.00")]
        [InlineData(0, "$0.00")]
        public void AmountsInCents(decimal value, string expected)
        {
            var context = CreateContext(options => options.MoneyOptions.AmountsInCents = true);

            Assert.Equal(expected, Invoke(MoneyFilters.Money, value, context));
        }

        [Fact]
        public void CurrencyCanBeConfiguredInOptions()
        {
            var context = CreateContext(options => options.MoneyOptions.Currency = "EUR");

            Assert.Equal("€10.00", Invoke(MoneyFilters.Money, 10, context));
            Assert.Equal("€10.00 EUR", Invoke(MoneyFilters.MoneyWithCurrency, 10, context));
        }

        [Fact]
        public void CurrencyCanBePassedAsAPositionalArgument()
        {
            var arguments = new FilterArguments(new StringValue("EUR"));

            Assert.Equal("€10.00", Invoke(MoneyFilters.Money, 10, CreateContext(), arguments));
        }

        [Fact]
        public void CurrencyCanBePassedAsANamedArgument()
        {
            var arguments = new FilterArguments().Add("currency", new StringValue("EUR"));

            Assert.Equal("€10.00", Invoke(MoneyFilters.Money, 10, CreateContext(), arguments));
        }

        [Fact]
        public void CurrencyArgumentTakesPrecedenceOverOptions()
        {
            var context = CreateContext(options => options.MoneyOptions.Currency = "EUR");
            var arguments = new FilterArguments(new StringValue("GBP"));

            Assert.Equal("£10.00 GBP", Invoke(MoneyFilters.MoneyWithCurrency, 10, context, arguments));
        }

        [Fact]
        public void UnknownCurrencyUsesItsCodeAsSymbol()
        {
            var arguments = new FilterArguments(new StringValue("XYZ"));

            Assert.Equal("XYZ10.00", Invoke(MoneyFilters.Money, 10, CreateContext(), arguments));
        }

        [Fact]
        public void CurrencyCanBeAddedToOptions()
        {
            var context = CreateContext(options => options.MoneyOptions.Currencies["XYZ"] = new MoneyCurrency("XYZ", "Ξ", 4));
            var arguments = new FilterArguments(new StringValue("XYZ"));

            Assert.Equal("Ξ10.0000", Invoke(MoneyFilters.Money, 10, context, arguments));
        }

        [Theory]
        [InlineData(10, "¥10")]
        [InlineData(1234.56, "¥1,235")]
        public void CurrenciesWithoutDecimalDigits(decimal value, string expected)
        {
            var arguments = new FilterArguments(new StringValue("JPY"));

            Assert.Equal(expected, Invoke(MoneyFilters.Money, value, CreateContext(), arguments));
        }

        [Theory]
        [InlineData("{{amount}}", "1,134.65")]
        [InlineData("{{amount_no_decimals}}", "1,135")]
        [InlineData("{{amount_with_comma_separator}}", "1.134,65")]
        [InlineData("{{amount_no_decimals_with_comma_separator}}", "1.135")]
        [InlineData("{{amount_with_apostrophe_separator}}", "1'134.65")]
        [InlineData("{{amount_no_decimals_with_space_separator}}", "1 135")]
        [InlineData("{{amount_with_space_separator}}", "1 134,65")]
        [InlineData("{{amount_with_period_and_space_separator}}", "1 134.65")]
        [InlineData("${{ amount }}", "$1,134.65")]
        [InlineData("{{amount_with_comma_separator}} kr", "1.134,65 kr")]
        [InlineData("{{currency_symbol}}{{amount}} {{currency}}", "$1,134.65 USD")]
        [InlineData("{{unknown}} {{amount}}", "{{unknown}} 1,134.65")]
        [InlineData("no placeholder", "no placeholder")]
        public void MoneyFormatIsUsedWhenDefined(string format, string expected)
        {
            var context = CreateContext(options => options.MoneyOptions.MoneyFormat = format);

            Assert.Equal(expected, Invoke(MoneyFilters.Money, 1134.65M, context));
        }

        [Fact]
        public void MoneyWithCurrencyFormatIsUsedWhenDefined()
        {
            var context = CreateContext(options => options.MoneyOptions.MoneyWithCurrencyFormat = "{{amount}} {{currency}}");

            Assert.Equal("1,134.65 USD", Invoke(MoneyFilters.MoneyWithCurrency, 1134.65M, context));
        }

        [Fact]
        public void MoneyWithCurrencyFallsBackToMoneyFormat()
        {
            var context = CreateContext(options => options.MoneyOptions.MoneyFormat = "{{amount}} kr");

            Assert.Equal("1,134.65 kr USD", Invoke(MoneyFilters.MoneyWithCurrency, 1134.65M, context));
        }

        [Theory]
        [InlineData(10, "$10")]
        [InlineData(10.5, "$10.50")]
        public void MoneyWithoutTrailingZerosUsesTheNoDecimalsVariantOfTheFormat(decimal value, string expected)
        {
            var context = CreateContext(options => options.MoneyOptions.MoneyFormat = "${{amount}}");

            Assert.Equal(expected, Invoke(MoneyFilters.MoneyWithoutTrailingZeros, value, context));
        }

        [Fact]
        public void MoneyWithoutCurrencyIgnoresTheMoneyFormat()
        {
            var context = CreateContext(options => options.MoneyOptions.MoneyFormat = "${{amount}}");

            Assert.Equal("1,134.65", Invoke(MoneyFilters.MoneyWithoutCurrency, 1134.65M, context));
        }

        [Fact]
        public void EnUsCultureUsesItsOwnCurrency()
        {
            var context = CreateContext(options => options.CultureInfo = new CultureInfo("en-US"));

            Assert.Equal("$1,134.65", Invoke(MoneyFilters.Money, 1134.65M, context));
            Assert.Equal("$1,134.65 USD", Invoke(MoneyFilters.MoneyWithCurrency, 1134.65M, context));
            Assert.Equal("1,134.65", Invoke(MoneyFilters.MoneyWithoutCurrency, 1134.65M, context));
        }

        [Theory]
        [InlineData("de-DE", "1.134,65 €", "EUR")]
        [InlineData("fr-FR", "1 134,65 €", "EUR")]
        [InlineData("en-GB", "£1,134.65", "GBP")]
        [InlineData("ja-JP", "¥1,135", "JPY")]
        public void CulturesUseTheirOwnCurrency(string cultureName, string expected, string expectedCode)
        {
            var context = CreateContext(options => options.CultureInfo = new CultureInfo(cultureName));

            Assert.Equal(expected, Normalize(Invoke(MoneyFilters.Money, 1134.65M, context)));
            Assert.Equal(expected + " " + expectedCode, Normalize(Invoke(MoneyFilters.MoneyWithCurrency, 1134.65M, context)));
        }

        [Fact]
        public void NeutralCulturesResolveTheirCurrency()
        {
            var context = CreateContext(options => options.CultureInfo = new CultureInfo("de"));

            Assert.EndsWith("EUR", Invoke(MoneyFilters.MoneyWithCurrency, 10, context));
        }

        [Theory]
        [InlineData("en-US")]
        [InlineData("de-DE")]
        [InlineData("fr-FR")]
        public void MoneyShouldNotBeAffectedByCurrentCulture(string culture)
        {
            var previousCulture = CultureInfo.CurrentCulture;
            var previousUICulture = CultureInfo.CurrentUICulture;

            try
            {
                CultureInfo.CurrentCulture = CultureInfo.GetCultureInfo(culture);
                CultureInfo.CurrentUICulture = CultureInfo.GetCultureInfo(culture);

                Assert.Equal("$1,134.65", Invoke(MoneyFilters.Money, 1134.65M));
            }
            finally
            {
                CultureInfo.CurrentCulture = previousCulture;
                CultureInfo.CurrentUICulture = previousUICulture;
            }
        }

        [Fact]
        public void EmptyValuesRenderAnEmptyString()
        {
            var context = CreateContext();

            Assert.Equal("", MoneyFilters.Money(NilValue.Instance, FilterArguments.Empty, context).Result.ToStringValue());
            Assert.Equal("", MoneyFilters.Money(BlankValue.Instance, FilterArguments.Empty, context).Result.ToStringValue());
            Assert.Equal("", MoneyFilters.Money(EmptyValue.Instance, FilterArguments.Empty, context).Result.ToStringValue());
            Assert.Equal("", MoneyFilters.MoneyWithCurrency(NilValue.Instance, FilterArguments.Empty, context).Result.ToStringValue());
            Assert.Equal("", MoneyFilters.MoneyWithoutCurrency(NilValue.Instance, FilterArguments.Empty, context).Result.ToStringValue());
            Assert.Equal("", MoneyFilters.MoneyWithoutTrailingZeros(NilValue.Instance, FilterArguments.Empty, context).Result.ToStringValue());
        }

        [Fact]
        public void StringValuesAreParsed()
        {
            Assert.Equal("$10.50", MoneyFilters.Money(new StringValue("10.50"), FilterArguments.Empty, CreateContext()).Result.ToStringValue());
        }

        [Fact]
        public void TooManyArgumentsThrows()
        {
            var arguments = new FilterArguments(new StringValue("EUR"), new StringValue("USD"));

            Assert.Throws<FluidException>(() => MoneyFilters.Money(NumberValue.Create(10), arguments, CreateContext()).Result);
        }

        [Theory]
        [InlineData("{{ 1134.65 | money }}", "$1,134.65")]
        [InlineData("{{ 1134.65 | money_with_currency }}", "$1,134.65 USD")]
        [InlineData("{{ 1134.65 | money_without_currency }}", "1,134.65")]
        [InlineData("{{ 10.00 | money_without_trailing_zeros }}", "$10")]
        [InlineData("{{ 10 | money: 'EUR' }}", "€10.00")]
        [InlineData("{{ 10 | money: currency: 'EUR' }}", "€10.00")]
        [InlineData("{{ 10 | money: 'XYZ' }}", "XYZ10.00")]
        [InlineData("{{ nil | money }}", "")]
        public void MoneyFiltersAreRegisteredByWithMoneyFilters(string source, string expected)
        {
            var options = new TemplateOptions();
            options.Filters.WithMoneyFilters();

            Assert.True(_parser.TryParse(source, out var template, out var errors), errors);
            Assert.Equal(expected, template.Render(new TemplateContext(options)));
        }

        [Fact]
        public void MoneyFiltersAreNotRegisteredByDefault()
        {
            var options = new TemplateOptions { StrictFilters = true };

            Assert.True(_parser.TryParse("{{ 10 | money }}", out var template, out var errors), errors);
            Assert.Throws<FluidException>(() => template.Render(new TemplateContext(options)));
        }

        [Fact]
        public void MoneyOptionsCanBeOverriddenPerContext()
        {
            var options = new TemplateOptions();
            options.Filters.WithMoneyFilters();

            var context = new TemplateContext(options)
            {
                MoneyOptions = new MoneyOptions { Currency = "EUR" }
            };

            Assert.True(_parser.TryParse("{{ 10 | money }}", out var template, out var errors), errors);
            Assert.Equal("€10.00", template.Render(context));
        }

        private static TemplateContext CreateContext(Action<TemplateOptions> configure = null)
        {
            var options = new TemplateOptions();
            configure?.Invoke(options);

            return new TemplateContext(options);
        }

        private static string Invoke(FilterDelegate filter, decimal value, TemplateContext context = null, FilterArguments arguments = null)
        {
            context ??= CreateContext();

            return filter(NumberValue.Create(value), arguments ?? FilterArguments.Empty, context).Result.ToStringValue();
        }

        /// <summary>
        /// Replaces the non-breaking spaces some cultures use as separators so that the assertions
        /// don't depend on the ICU version of the host.
        /// </summary>
        private static string Normalize(string value)
        {
            return value.Replace(' ', ' ').Replace(' ', ' ');
        }
    }
}
