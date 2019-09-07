using System.Globalization;
using Fluid.Filters;
using Fluid.Values;
using Shouldly;
using Xunit;

namespace Fluid.Tests
{
    public class MoneyFilterTests
    {
        [Theory]
        [InlineData("59.65", "59.65", "en-AU")]
        [InlineData("59.65", "59.65", "en-GB")]
        [InlineData("59.65", "59.65", "en-US")]
        public void Money(string format, string expected, string cultureInfo)
        {
            // Shopify syntax for Money is configuration based, but default appears to be money_without_currency so matching
            var culture = new CultureInfo(cultureInfo);
            var context = new TemplateContext { CultureInfo = culture };
            var input = new StringValue(format);
            var arguments = new FilterArguments(new StringValue(format));
            var result = MoneyFilters.Money(input, arguments, context);
            result.ToStringValue().ShouldBe(expected);
        }

        [Theory]
        [InlineData("59.65", "59.65", "en-AU")]
        [InlineData("59.65", "59.65", "en-GB")]
        [InlineData("59.65", "59.65", "en-US")]
        public void Money_Without_Currency(string format, string expected, string cultureInfo)
        {
            var culture = new CultureInfo(cultureInfo);
            var context = new TemplateContext { CultureInfo = culture };
            var input = new StringValue(format);
            var arguments = new FilterArguments(new StringValue(format));
            var result = MoneyFilters.MoneyWithOutCurrency(input, arguments, context);
            result.ToStringValue().ShouldBe(expected);
        }

        [Theory]
        [InlineData("59.65", "$59.65", "en-AU")]
        [InlineData("59.65", "£59.65", "en-GB")]
        [InlineData("59.65", "$59.65", "en-US")]
        public void MoneyWithCurrency(string format, string expected, string cultureInfo)
        {
            //Shopify syntax for Money is configuration based (in shopify), default appears to be money_without_currency
            var culture = new CultureInfo(cultureInfo);
            var context = new TemplateContext { CultureInfo = culture };
            var input = new StringValue(format);
            var arguments = new FilterArguments(new StringValue(format));
            var result = MoneyFilters.MoneyWithCurrency(input, arguments, context);
            result.ToStringValue().ShouldBe(expected);
        }

        [Theory]
        [InlineData("59.65", "$59.65", "en-AU")]
        [InlineData("59.65", "£59.65", "en-GB")]
        [InlineData("59.65", "$59.65", "en-US")]
        [InlineData("59.01", "$59.01", "en-US")]
        [InlineData("59.00", "$59", "en-US")]
        [InlineData("59.000001", "$59", "en-US")] //currency rounding 2 decimals
        [InlineData("59.009", "$59.01", "en-US")] //currency rounding 2 decimals
        public void MoneyWithoutTrailingZeros(string format, string expected, string cultureInfo)
        {
            var culture = new CultureInfo(cultureInfo);
            var context = new TemplateContext { CultureInfo = culture };
            var input = new StringValue(format);
            var arguments = new FilterArguments(new StringValue(format));
            var result = MoneyFilters.MoneyWithoutTrailingZeros(input, arguments, context);
            result.ToStringValue().ShouldBe(expected);
        }
    }
}