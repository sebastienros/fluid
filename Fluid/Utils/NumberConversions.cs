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
#if NET8_0_OR_GREATER
        // Lengths, offsets and limits are nearly always small whole numbers, which need neither rounding nor the
        // decimal comparisons below, that cost several times more than reading the bits.
        Span<int> bits = stackalloc int[4];
        decimal.GetBits(value, bits);

        // A scale of 0 (bits 16 to 23 of the flags) and a magnitude that fits in 31 bits.
        if ((bits[3] & 0x00FF0000) == 0 && bits[2] == 0 && bits[1] == 0 && bits[0] >= 0)
        {
            return bits[3] < 0 ? -bits[0] : bits[0];
        }
#endif

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
    /// Converts a value to an integer like Liquid's <c>Utils.to_integer</c>, which the arguments of <c>slice</c> go
    /// through: a number must be an integer, and any other value is converted to a string
    /// read like Ruby's <c>Integer()</c> (see <see cref="TryParseInt32Saturated(string, out int)"/>). A value out of an
    /// int's range gives the nearest int, like <see cref="ToInt32Saturated(decimal)"/> does.
    /// </summary>
    public static bool TryToInt32Saturated(FluidValue value, TemplateContext context, out int result)
    {
        switch (value)
        {
            case NumberValue number:
                if (NumberValue.GetScale(number.Value) == 0)
                {
                    result = ToInt32Saturated(number.Value);
                    return true;
                }

                result = 0;
                return false;

            case BigNumberValue big:
                result = big.Sign > 0 ? int.MaxValue : int.MinValue;
                return big.IsIntegral;

            case { Type: FluidValues.Number }:
                // A custom number.
                var custom = value.ToNumberValue(context);
                result = ToInt32Saturated(custom);
                return NumberValue.GetScale(custom) == 0;

            default:
                result = 0;
                return !value.IsNil() && TryParseInt32Saturated(value.ToStringValue(context), out result);
        }
    }

    /// <summary>
    /// Parses an integer like Ruby's <c>Integer()</c>, which Liquid uses for integer arguments and loop attributes, and
    /// gives the nearest int for a value out of range, like <see cref="ToInt32Saturated(decimal)"/> does for numbers.
    /// Like in Ruby, the digits can be surrounded by ASCII whitespace, follow a sign and a <c>0x</c>, <c>0b</c>,
    /// <c>0o</c> or <c>0d</c> prefix (a leading <c>0</c> alone means octal), and be separated by single underscores:
    /// <c>"0x1F"</c> is 31, <c>"010"</c> is 8 and <c>"1_000"</c> is 1000. An underscore can't follow a prefix.
    /// </summary>
    public static bool TryParseInt32Saturated(string value, out int result)
    {
        result = 0;

        if (value is null)
        {
            return false;
        }

        var s = StringValue.TrimAsciiWhiteSpace(value.AsSpan());
        var i = 0;

        var negative = false;
        if (i < s.Length && s[i] is '+' or '-')
        {
            negative = s[i] == '-';
            i++;
        }

        var radix = 10;
        if (i + 1 < s.Length && s[i] == '0')
        {
            switch (s[i + 1])
            {
                case 'x' or 'X': radix = 16; i += 2; break;
                case 'b' or 'B': radix = 2; i += 2; break;
                case 'o' or 'O': radix = 8; i += 2; break;
                case 'd' or 'D': radix = 10; i += 2; break;
                // A leading zero alone is a prefix too: like after the other ones, "0_7" is invalid and "00_7" is 7.
                default: radix = 8; i += 1; break;
            }
        }

        var start = i;

        // Stops growing once out of an int's range: the largest it gets is (int.MaxValue + 1) * 16 + 15.
        long magnitude = 0;

        for (; i < s.Length; i++)
        {
            var c = s[i];

            if (c == '_')
            {
                // Only single underscores between digits.
                if (i == start || s[i - 1] == '_' || i + 1 == s.Length)
                {
                    return false;
                }

                continue;
            }

            var digit = c switch
            {
                >= '0' and <= '9' => c - '0',
                >= 'a' and <= 'f' => c - 'a' + 10,
                >= 'A' and <= 'F' => c - 'A' + 10,
                _ => int.MaxValue,
            };

            if (digit >= radix)
            {
                return false;
            }

            if (magnitude <= int.MaxValue)
            {
                magnitude = magnitude * radix + digit;
            }
        }

        if (i == start)
        {
            return false;
        }

        if (negative)
        {
            result = magnitude > -(long)int.MinValue ? int.MinValue : (int)-magnitude;
        }
        else
        {
            result = magnitude > int.MaxValue ? int.MaxValue : (int)magnitude;
        }

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
            return ToNumberValue(value);
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
    /// Creates the number of a double, like <c>NumberValue.Create(ToDecimal(value))</c>.
    /// </summary>
    /// <exception cref="OverflowException">The value is NaN, an infinity, or out of the decimal range.</exception>
    public static NumberValue ToNumberValue(double value)
    {
        // Whole numbers, like quantities and many prices, are exact with 15 significant digits: their decimal is the
        // integer, with a scale of 0, so the arithmetic of ToDecimal isn't needed. NaN fails both comparisons.
        if (value > int.MinValue && value < int.MaxValue)
        {
            var integer = (int)value;

            if (integer == value)
            {
                return NumberValue.Create(integer);
            }
        }

        return NumberValue.Create(ToDecimal(value));
    }

    /// <summary>
    /// Creates the number of a float, like <c>NumberValue.Create(ToDecimal(value))</c>.
    /// </summary>
    /// <exception cref="OverflowException">The value is NaN, an infinity, or out of the decimal range.</exception>
    public static NumberValue ToNumberValue(float value)
    {
        // Whole numbers with at most 7 digits are exact with the 7 significant digits of a float, see the double overload.
        if (value > -1e7f && value < 1e7f)
        {
            var integer = (int)value;

            if (integer == value)
            {
                return NumberValue.Create(integer);
            }
        }

        return NumberValue.Create(ToDecimal(value));
    }

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
