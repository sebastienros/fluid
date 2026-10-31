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
            return NumberValue.Create((decimal)value);
        }

        return StringValue.Create(value.ToString("R", CultureInfo.InvariantCulture));
    }
}
