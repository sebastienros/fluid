using System.Globalization;
using System.Text.Encodings.Web;

namespace Fluid.Values;

/// <summary>
/// A <see cref="TimeSpan"/>, rendered like <c>13:00:00</c>. The date filters read it as a time of day on
/// January 1, 1970, at the standard offset of the time zone of the template context.
/// </summary>
public sealed class TimeSpanValue : FluidValue
{
    private static readonly DateTime Epoch = new(1970, 1, 1, 0, 0, 0, DateTimeKind.Unspecified);

    private readonly TimeSpan _value;

    public static TimeSpanValue Create(TimeSpan value) => new TimeSpanValue(value);

    private TimeSpanValue(TimeSpan value)
    {
        _value = value;
    }

    internal TimeSpan Value => _value;

    public override FluidValues Type => FluidValues.DateTime;

    /// <summary>
    /// Reads the time span as a time of day on January 1, 1970, in the time zone of the context.
    /// The time zone is applied here rather than when the value is created, so the clock time is
    /// kept and <see cref="TemplateContext.TimeZone"/> is used even when it differs from the options.
    /// </summary>
    internal bool TryGetDateTimeOffset(TemplateContext context, out DateTimeOffset result)
    {
        try
        {
            // A time of day isn't a moment: the current standard offset of the time zone is used,
            // not the one it had in 1970 (Istanbul was at +02:00, it is at +03:00), nor daylight saving time.
            result = new DateTimeOffset(Epoch.Add(_value), context.TimeZone.BaseUtcOffset);
            return true;
        }
        catch (ArgumentOutOfRangeException)
        {
            // Too long a time span for a date.
            result = default;
            return false;
        }
    }

    public override bool Equals(FluidValue other)
    {
        return other is TimeSpanValue timeSpanValue && _value.Equals(timeSpanValue._value);
    }

    public override bool ToBooleanValue(TemplateContext context)
    {
        return true;
    }

    public override decimal ToNumberValue(TemplateContext context)
    {
        // Like Liquid, where Utils.to_number returns 0 for anything but a number or a string.
        return 0;
    }

    public override string ToStringValue(TemplateContext context)
    {
        return _value.ToString("c", CultureInfo.InvariantCulture);
    }

    public override ValueTask WriteToAsync(IFluidOutput output, TextEncoder encoder, TemplateContext context)
    {
        AssertWriteToParameters(output, encoder, context);
        output.Write(ToStringValue(context));
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

        return obj is TimeSpan otherValue && _value.Equals(otherValue);
    }

    public override int GetHashCode()
    {
        return _value.GetHashCode();
    }
}
