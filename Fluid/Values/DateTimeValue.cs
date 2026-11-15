using System.Globalization;
using System.Text.Encodings.Web;

namespace Fluid.Values;

/// <summary>
/// A moment, like a Ruby <c>Time</c>. It renders like <c>Time#to_s</c>, with its own offset: <c>2026-03-04 13:00:00 -0500</c>,
/// or <c>2026-03-04 18:00:00 UTC</c> when created from a <see cref="DateTime"/> of <see cref="DateTimeKind.Utc"/> kind,
/// like <c>Time.utc</c>.
/// </summary>
public sealed class DateTimeValue : FluidValue
{
    private readonly DateTimeOffset _value;

    // Created from a DateTime of Utc kind, rendered with "UTC" rather than "+0000" like Ruby's Time.utc.
    private readonly bool _utc;

    public static DateTimeValue Create(DateTimeOffset value) => new DateTimeValue(value);

    /// <summary>
    /// Creates a value from a moment. A <see cref="DateTime"/> of <see cref="DateTimeKind.Unspecified"/> kind is read in
    /// the time zone of the machine: <see cref="FluidValue.Create(object, TemplateOptions)"/> converts it to a
    /// <see cref="PlainDateTimeValue"/> instead, read in the time zone of the template context.
    /// </summary>
    public static DateTimeValue Create(DateTime value) => new DateTimeValue(value);

    private DateTimeValue(DateTimeOffset value)
    {
        _value = value;
    }

    private DateTimeValue(DateTime value)
    {
        _utc = value.Kind == DateTimeKind.Utc;

        // Handle edge cases where DateTime cannot be safely converted to DateTimeOffset
        // with local timezone offset due to overflow (e.g., DateTime.MinValue with positive offset)

        // Check if the value is within one day of the boundaries where overflow might occur
        if (value <= DateTime.MinValue.AddDays(1))
        {
            // Value is close to MinValue - attempt conversion with try-catch
            try
            {
                _value = value;
            }
            catch (ArgumentOutOfRangeException)
            {
                // Offset caused underflow - use minimum boundary
                _value = DateTimeOffset.MinValue;
            }
        }
        else if (value >= DateTime.MaxValue.AddDays(-1))
        {
            // Value is close to MaxValue - attempt conversion with try-catch
            try
            {
                _value = value;
            }
            catch (ArgumentOutOfRangeException)
            {
                // Offset caused overflow - use maximum boundary
                _value = DateTimeOffset.MaxValue;
            }
        }
        else
        {
            // Normal case - direct conversion without try-catch overhead
            _value = value;
        }
    }

    public override FluidValues Type => FluidValues.DateTime;

    public override bool Equals(FluidValue other)
    {
        // Other values have the DateTime type too, like a TimeSpanValue or a PlainDateTimeValue.
        return other is DateTimeValue dateTimeValue && _value.Equals(dateTimeValue._value);
    }

    public override bool ToBooleanValue(TemplateContext context)
    {
        return true;
    }

    public override decimal ToNumberValue(TemplateContext context)
    {
        // Like Liquid, a date is not a number: math is done on its Unix time, with date: '%s'.
        return 0;
    }

    public override string ToStringValue(TemplateContext context)
    {
        return Format(_value, _utc);
    }

    /// <summary>
    /// Formats a moment like Ruby's <c>Time#to_s</c>: <c>2026-03-04 13:00:00 -0500</c>, or <c>2026-03-04 18:00:00 UTC</c>.
    /// </summary>
    internal static string Format(DateTimeOffset value, bool utc)
    {
        var dateTime = value.ToString("yyyy-MM-dd HH:mm:ss", CultureInfo.InvariantCulture);

        if (utc && value.Offset == TimeSpan.Zero)
        {
            return dateTime + " UTC";
        }

        var offset = value.Offset;
        var sign = offset < TimeSpan.Zero ? '-' : '+';
        offset = offset.Duration();

        return dateTime + " " + sign + offset.Hours.ToString("00", CultureInfo.InvariantCulture) + offset.Minutes.ToString("00", CultureInfo.InvariantCulture);
    }

    public override ValueTask WriteToAsync(IFluidOutput output, TextEncoder encoder, TemplateContext context)
    {
        AssertWriteToParameters(output, encoder, context);
        output.Write(Format(_value, _utc));
        return default;
    }

    public override object ToObjectValue(TemplateContext context)
    {
        return _value;
    }

    public override bool Equals(object obj)
    {
        if (obj is FluidValue fluidValue)
        {
            return Equals(fluidValue);
        }

        // The is operator will return false if null
        if (obj is DateTimeOffset otherValue)
        {
            return _value.Equals(otherValue);
        }

        return false;
    }

    public override int GetHashCode()
    {
        return _value.GetHashCode();
    }
}
