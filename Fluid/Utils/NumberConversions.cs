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
