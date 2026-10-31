using System;
using System.Globalization;
using System.Threading.Tasks;
using Fluid.Values;
using Xunit;

namespace Fluid.Tests;

/// <summary>
/// Doubles and floats become decimals with 15 and 7 significant digits, like the decimal cast did
/// before .NET 11, which now keeps their exact binary value (0.1 would be 0.1000000000000000055511151231).
/// </summary>
public class FloatingPointConversionTests
{
    private static readonly FluidParser _parser = new FluidParser();

    public sealed class Measurements
    {
        public double Double { get; set; }

        public float Single { get; set; }
    }

    // Exposes the conversions used by source generated accessors.
    private sealed class Converter : MemberAccessor
    {
        public override ValueTask<FluidValue> GetAsync(object obj, string name, TemplateContext context) => default;

        public static FluidValue Convert(double value, TemplateContext context) => CreateValueTask(value, context).Result;

        public static FluidValue Convert(float value, TemplateContext context) => CreateValueTask(value, context).Result;
    }

    [Theory]
    [InlineData(123.12, "123.12")]
    [InlineData(-123.12, "-123.12")]
    [InlineData(0.1, "0.1")]
    [InlineData(0.1 + 0.2, "0.3")]
    [InlineData(1.0 / 3, "0.333333333333333")]
    [InlineData(2.0, "2")]
    [InlineData(1e20, "100000000000000000000")]
    [InlineData(1e-5, "1.0e-05")]
    public async Task DoublesKeepFifteenSignificantDigits(double value, string expected)
    {
        var context = new TemplateContext();

        Assert.Equal(expected, FluidValue.Create(value, context.Options).ToStringValue(context));
        Assert.Equal(expected, Converter.Convert(value, context).ToStringValue(context));

        context.SetValue("m", new Measurements { Double = value });
        Assert.Equal(expected, await _parser.Parse("{{ m.Double }}").RenderAsync(context));
    }

    [Theory]
    [InlineData(1.2f, "1.2")]
    [InlineData(19.99f, "19.99")]
    [InlineData(-0.001f, "-0.001")]
    public void FloatsKeepSevenSignificantDigits(float value, string expected)
    {
        var context = new TemplateContext();

        Assert.Equal(expected, Converter.Convert(value, context).ToStringValue(context));
    }

    [Fact]
    public void NaNIsOutOfTheDecimalRange()
    {
        var context = new TemplateContext();

        Assert.Throws<OverflowException>(() => Converter.Convert(double.NaN, context));
        Assert.Throws<OverflowException>(() => Converter.Convert(float.NaN, context));
    }

#if !NET11_0_OR_GREATER
    [Fact]
    public void ConversionsMatchTheDecimalCast()
    {
        var context = new TemplateContext();
        var random = new Random(42);

        for (var i = 0; i < 200_000; i++)
        {
            // Random mantissas over every exponent that fits a decimal, from rounding to zero to overflowing.
            var bits = ((long)random.Next() << 32 | (uint)random.Next()) & 0x000F_FFFF_FFFF_FFFF;
            var exponent = random.Next(1023 - 100, 1023 + 100);
            var value = BitConverter.Int64BitsToDouble(bits | (long)exponent << 52);
            value = random.Next(2) == 0 ? value : -value;

            AssertSameConversion(value, () => (decimal)value, () => Converter.Convert(value, context).ToNumberValue(context));

            var single = (float)value;
            AssertSameConversion(value, () => (decimal)single, () => Converter.Convert(single, context).ToNumberValue(context));

            // Values with few digits, which keep trailing zeros to drop.
            var rounded = Math.Round(value, random.Next(0, 16));
            AssertSameConversion(rounded, () => (decimal)rounded, () => Converter.Convert(rounded, context).ToNumberValue(context));
        }
    }

    private static void AssertSameConversion(double value, Func<decimal> cast, Func<decimal> conversion)
    {
        decimal expected;

        try
        {
            expected = cast();
        }
        catch (OverflowException)
        {
            Assert.Throws<OverflowException>(() => conversion());
            return;
        }

        // The text includes the scale, which equality ignores.
        var expectedText = expected.ToString(CultureInfo.InvariantCulture);
        var actualText = conversion().ToString(CultureInfo.InvariantCulture);

        if (expectedText != actualText)
        {
            Assert.Fail($"{value.ToString("R", CultureInfo.InvariantCulture)}: expected {expectedText}, got {actualText}");
        }
    }
#endif
}
