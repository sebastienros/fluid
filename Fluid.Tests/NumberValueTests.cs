using Fluid.Values;
using System.Globalization;
using System.Threading.Tasks;
using Xunit;

namespace Fluid.Tests;

public class NumberValueTests
{
    [Theory]
    [InlineData("5", "5")]
    [InlineData("123456", "123456")]
    [InlineData("-7", "-7")]
    [InlineData("19.99", "19.99")]
    [InlineData("-19.99", "-19.99")]
    [InlineData("0.5", "0.5")]
    [InlineData("1.50", "1.5")]
    [InlineData("0.0001", "0.0001")]
    [InlineData("2.0", "2.0")]
    [InlineData("20.00", "20.0")]
    [InlineData("-3.000", "-3.0")]
    [InlineData("0.0", "0.0")]
    [InlineData("7922816251426433759354395033.5", "7922816251426433759354395033.5")]
    [InlineData("79228162514264337593543950.00", "79228162514264337593543950.0")]
    public async Task ShouldWriteDecimals(string value, string expected)
    {
        // Parsed from text so the scale is the one written in the test data.
        var number = NumberValue.Create(decimal.Parse(value, CultureInfo.InvariantCulture));
        var template = new FluidParser().Parse("{{ value }}");

        var result = await template.RenderAsync(new TemplateContext().SetValue("value", number));

        Assert.Equal(expected, result);
    }
}
