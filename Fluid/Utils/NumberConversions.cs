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
    /// Reads the leading integer of a string like Ruby's <c>String#to_i</c>: leading whitespace, an optional sign,
    /// then digits (single underscores between them are allowed). Anything else ends the number, and a string
    /// without digits is 0. Values out of range give the nearest int.
    /// </summary>
    public static int ParseLeadingInt32Saturated(string value)
    {
        var i = 0;
        while (i < value.Length && value[i] is ' ' or '\t' or '\n' or '\v' or '\f' or '\r')
        {
            i++;
        }

        var negative = false;
        if (i < value.Length && value[i] is '-' or '+')
        {
            negative = value[i] == '-';
            i++;
        }

        long result = 0;
        for (; i < value.Length; i++)
        {
            var c = value[i];
            if (c == '_' && i > 0 && value[i - 1] is >= '0' and <= '9' && i + 1 < value.Length && value[i + 1] is >= '0' and <= '9')
            {
                continue;
            }

            if (c is < '0' or > '9')
            {
                break;
            }

            // Past int.MaxValue + 1 the result only saturates.
            if (result <= (long)int.MaxValue + 1)
            {
                result = result * 10 + (c - '0');
            }
        }

        result = negative ? -result : result;
        return (int)Math.Max(int.MinValue, Math.Min(int.MaxValue, result));
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
            return NumberValue.Create((decimal)value);
        }

        return StringValue.Create(value.ToString("R", CultureInfo.InvariantCulture));
    }
}
