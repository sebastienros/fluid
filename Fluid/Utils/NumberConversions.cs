using System.Globalization;
using Fluid.Values;

namespace Fluid.Utils;

/// <summary>
/// Narrowing conversions for numbers that come from templates or models, which can be outside the
/// range of the target type.
/// </summary>
internal static class NumberConversions
{
    /// <summary>
    /// Rounds like <see cref="Convert.ToInt32(decimal)"/>, but gives the nearest int for a value out
    /// of range instead of throwing. For offsets, limits and lengths, where a huge value means
    /// "all" or "none".
    /// </summary>
    public static int ToInt32Saturated(decimal value)
    {
        if (value >= int.MaxValue)
        {
            return int.MaxValue;
        }

        if (value <= int.MinValue)
        {
            return int.MinValue;
        }

        return Convert.ToInt32(value);
    }

    /// <summary>
    /// Parses an integer, like Ruby's <c>Integer()</c> which Liquid uses for loop attributes, and gives the
    /// nearest int for a value out of range, like <see cref="ToInt32Saturated(decimal)"/> does for numbers.
    /// </summary>
    public static bool TryParseInt32Saturated(string value, out int result)
    {
        if (int.TryParse(value, NumberStyles.Integer, CultureInfo.InvariantCulture, out result))
        {
            return true;
        }

        if (decimal.TryParse(value, NumberStyles.Integer, CultureInfo.InvariantCulture, out var number))
        {
            result = ToInt32Saturated(number);
            return true;
        }

        // Even more digits than a decimal holds.
        var span = value.AsSpan().Trim();
        var negative = span.Length > 0 && span[0] == '-';
        var digits = span.Length > 0 && span[0] is '-' or '+' ? span.Slice(1) : span;

        if (digits.Length == 0)
        {
            return false;
        }

        foreach (var c in digits)
        {
            if (c is < '0' or > '9')
            {
                return false;
            }
        }

        result = negative ? int.MinValue : int.MaxValue;
        return true;
    }

    /// <summary>
    /// Truncates like an <c>(int)</c> cast, or returns false when the value is out of range.
    /// </summary>
    public static bool TryTruncateToInt32(decimal value, out int result)
    {
        if (value <= int.MinValue - 1m || value >= int.MaxValue + 1m)
        {
            result = 0;
            return false;
        }

        result = (int)value;
        return true;
    }

    /// <summary>
    /// Creates a value for a double. Numbers are decimals, which can't hold NaN, the infinities or
    /// magnitudes from about 7.9e28, so those are kept as their text instead.
    /// </summary>
    public static FluidValue CreateNumber(double value)
    {
        if (Math.Abs(value) < (double)decimal.MaxValue)
        {
            return NumberValue.Create(ToDecimal(value));
        }

        return StringValue.Create(value.ToString("R", CultureInfo.InvariantCulture));
    }

    // Double literals are correctly rounded, which Math.Pow doesn't guarantee.
    private static readonly double[] DoublePowersOf10 =
    [
        1e0, 1e1, 1e2, 1e3, 1e4, 1e5, 1e6, 1e7, 1e8, 1e9,
        1e10, 1e11, 1e12, 1e13, 1e14, 1e15, 1e16, 1e17, 1e18, 1e19,
        1e20, 1e21, 1e22, 1e23, 1e24, 1e25, 1e26, 1e27, 1e28,
    ];

    /// <summary>
    /// Converts like a <c>(decimal)</c> cast before .NET 11, which kept 15 significant digits: 0.1 is 0.1.
    /// Since .NET 11 the cast keeps the exact binary value, and 0.1 is 0.1000000000000000055511151231.
    /// </summary>
    /// <exception cref="OverflowException">The value is NaN, an infinity, or out of the decimal range.</exception>
    public static decimal ToDecimal(double value) => ToDecimal(value, 15);

    /// <summary>
    /// Converts like a <c>(decimal)</c> cast before .NET 11, which kept 7 significant digits.
    /// </summary>
    /// <exception cref="OverflowException">The value is NaN, an infinity, or out of the decimal range.</exception>
    public static decimal ToDecimal(float value) => ToDecimal(value, 7);

    // Port of VarDecFromR8 and VarDecFromR4 from the .NET 10 runtime. A float has the same unbiased
    // exponent as the double it widens to, so both read it from the double.
    private static decimal ToDecimal(double value, int digits)
    {
        // The largest scale, 10^28, is slightly more than 2^93: anything below 2^-95 rounds to zero.
        var exponent = (int)((BitConverter.DoubleToInt64Bits(value) >> 52) & 0x7FF) - 1022;

        if (exponent < -94)
        {
            return 0m;
        }

        if (exponent > 96)
        {
            throw new OverflowException("Value was either too large or too small for a Decimal.");
        }

        var negative = value < 0;
        var scaled = Math.Abs(value);

        // Scale to an integer of 'digits' digits. The power of ten is estimated from the exponent with
        // log10(2) * 2^16 = 19728.3, and can be one short.
        var scale = digits - 1 - ((exponent * 19728) >> 16);

        if (scale >= 0)
        {
            scale = Math.Min(scale, 28);
            scaled *= DoublePowersOf10[scale];
        }
        else if (scale != -1 || scaled >= DoublePowersOf10[digits])
        {
            scaled /= DoublePowersOf10[-scale];
        }
        else
        {
            scale = 0;
        }

        if (scaled < DoublePowersOf10[digits - 1] && scale < 28)
        {
            scaled *= 10;
            scale++;
        }

        // Halves round to even, like the runtime.
        var mantissa = (long)Math.Round(scaled);

        if (mantissa == 0)
        {
            return 0m;
        }

        if (scale < 0)
        {
            decimal result = mantissa;

            for (; scale < 0; scale++)
            {
                result *= 10;
            }

            return negative ? -result : result;
        }

        // Drop trailing zeros, but at most digits - 1 (the leading digit isn't zero) and never below scale 0.
        var removable = Math.Min(scale, digits - 1);

        if (removable >= 8 && mantissa % 100_000_000 == 0)
        {
            mantissa /= 100_000_000;
            scale -= 8;
            removable -= 8;
        }

        if (removable >= 4 && mantissa % 10_000 == 0)
        {
            mantissa /= 10_000;
            scale -= 4;
            removable -= 4;
        }

        if (removable >= 2 && mantissa % 100 == 0)
        {
            mantissa /= 100;
            scale -= 2;
            removable -= 2;
        }

        if (removable >= 1 && mantissa % 10 == 0)
        {
            mantissa /= 10;
            scale--;
        }

        return new decimal((int)mantissa, (int)(mantissa >> 32), 0, negative, (byte)scale);
    }
}
