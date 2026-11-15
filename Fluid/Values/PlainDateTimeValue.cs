using System.Globalization;
using System.Text.Encodings.Web;

namespace Fluid.Values;

/// <summary>
/// A date and time without a time zone, like a <see cref="DateTime"/> of <see cref="DateTimeKind.Unspecified"/> kind,
/// a <c>DateOnly</c> or a <c>TimeOnly</c>: a clock reading rather than a moment. A date and time is read in the time
/// zone of the template context, and renders like a Ruby <c>Time</c>: <c>2026-03-04 13:00:00 -0500</c>. A date is
/// like a Ruby <c>Date</c>, at midnight UTC, and renders <c>2026-03-04</c>. A time of day renders <c>13:00:00</c>.
/// </summary>
/// <remarks>
/// The name comes from the <c>PlainDateTime</c> type of JavaScript's Temporal API. A <see cref="DateTime"/> of
/// <see cref="DateTimeKind.Local"/> or <see cref="DateTimeKind.Utc"/> kind is a moment, and is a <see cref="DateTimeValue"/>.
/// </remarks>
public sealed class PlainDateTimeValue : FluidValue
{
    private enum PlainKind : byte
    {
        DateTime,
        Date,
        Time,
    }

    // Always of Unspecified kind. A time is on January 1, 0001.
    private readonly DateTime _value;
    private readonly PlainKind _kind;

    private PlainDateTimeValue(DateTime value, PlainKind kind)
    {
        _value = DateTime.SpecifyKind(value, DateTimeKind.Unspecified);
        _kind = kind;
    }

    /// <summary>
    /// Creates a value from the date and time of <paramref name="value"/>, whatever its <see cref="DateTime.Kind"/>.
    /// </summary>
    public static PlainDateTimeValue Create(DateTime value) => new PlainDateTimeValue(value, PlainKind.DateTime);

#if NET6_0_OR_GREATER
    /// <summary>
    /// Creates a value from a date. The date filters read it at midnight.
    /// </summary>
    public static PlainDateTimeValue Create(DateOnly value) => new PlainDateTimeValue(value.ToDateTime(TimeOnly.MinValue), PlainKind.Date);

    /// <summary>
    /// Creates a value from a time of day. The date filters read it on the current date of the context.
    /// </summary>
    public static PlainDateTimeValue Create(TimeOnly value) => new PlainDateTimeValue(DateTime.MinValue.Add(value.ToTimeSpan()), PlainKind.Time);
#endif

    /// <summary>
    /// Converts a <see cref="DateTime"/>: one of <see cref="DateTimeKind.Unspecified"/> kind has no time zone, and is read
    /// in the time zone of the context, the others are moments.
    /// </summary>
    internal static FluidValue FromDateTime(DateTime value)
    {
        return value.Kind == DateTimeKind.Unspecified
            ? new PlainDateTimeValue(value, PlainKind.DateTime)
            : DateTimeValue.Create(value);
    }

    public override FluidValues Type => FluidValues.DateTime;

    /// <summary>Whether the value is a date, like a <c>DateOnly</c>.</summary>
    internal bool IsDate => _kind == PlainKind.Date;

    /// <summary>Whether the value is a date and time, like a <see cref="DateTime"/>.</summary>
    internal bool IsDateTime => _kind == PlainKind.DateTime;

    /// <summary>Whether the value is a time of day, like a <c>TimeOnly</c>.</summary>
    internal bool IsTimeOfDay => _kind == PlainKind.Time;

    /// <summary>The date and time, of <see cref="DateTimeKind.Unspecified"/> kind. A time of day is on January 1, 0001.</summary>
    internal DateTime ClockReading => _value;

    /// <summary>
    /// Reads the value as a moment. A date and time is in the time zone of the context, with the offset at this date
    /// and time since it can vary with daylight saving time. A date is at midnight UTC, like a Ruby <c>Date</c>, which
    /// has no time zone. A time of day is on the current date of the context, like Ruby's <c>Time.parse("13:00")</c>.
    /// The time zone is applied here rather than when the value is created, so <see cref="TemplateContext.TimeZone"/>
    /// is used even when it differs from the options.
    /// </summary>
    internal bool TryGetDateTimeOffset(TemplateContext context, out DateTimeOffset result)
    {
        if (_kind == PlainKind.Date)
        {
            result = new DateTimeOffset(_value, TimeSpan.Zero);
            return true;
        }

        var dateTime = _kind == PlainKind.Time
            ? TimeZoneInfo.ConvertTime(context.Now(), context.TimeZone).Date.Add(_value.TimeOfDay)
            : _value;

        try
        {
            result = new DateTimeOffset(dateTime, context.TimeZone.GetUtcOffset(dateTime));
        }
        catch (ArgumentOutOfRangeException)
        {
            // Too close to DateTime.MinValue or MaxValue for the offset: keep the clock reading, in UTC.
            result = new DateTimeOffset(dateTime, TimeSpan.Zero);
        }

        return true;
    }

    public override bool Equals(FluidValue other)
    {
        return other is PlainDateTimeValue plain && _kind == plain._kind && _value == plain._value;
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
        return Format(context);
    }

    public override ValueTask WriteToAsync(IFluidOutput output, TextEncoder encoder, TemplateContext context)
    {
        AssertWriteToParameters(output, encoder, context);
        output.Write(Format(context));
        return default;
    }

    // A date and time like Ruby's Time#to_s, with the offset of the context's time zone: "2026-03-04 13:00:00 -0500".
    // A date like Date#to_s: "2026-03-04". A time of day has no Ruby equivalent and renders like a TimeSpan: "13:00:00".
    private string Format(TemplateContext context)
    {
        switch (_kind)
        {
            case PlainKind.Date:
                return _value.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture);
            case PlainKind.Time:
                return _value.ToString("HH:mm:ss", CultureInfo.InvariantCulture);
            default:
                TryGetDateTimeOffset(context, out var moment);
                return DateTimeValue.Format(moment, utc: false);
        }
    }

    /// <summary>
    /// Returns the <see cref="DateTime"/>, <c>DateOnly</c> or <c>TimeOnly</c> the value was created from.
    /// </summary>
    public override object ToObjectValue(TemplateContext context)
    {
        return _kind switch
        {
#if NET6_0_OR_GREATER
            PlainKind.Date => DateOnly.FromDateTime(_value),
            PlainKind.Time => TimeOnly.FromDateTime(_value),
#endif
            _ => _value,
        };
    }

    public override bool Equals(object obj)
    {
        return obj switch
        {
            FluidValue fluidValue => Equals(fluidValue),
            DateTime dateTime => _kind == PlainKind.DateTime && _value == dateTime,
#if NET6_0_OR_GREATER
            DateOnly date => _kind == PlainKind.Date && DateOnly.FromDateTime(_value) == date,
            TimeOnly time => _kind == PlainKind.Time && TimeOnly.FromDateTime(_value) == time,
#endif
            _ => false,
        };
    }

    public override int GetHashCode()
    {
        return HashCode.Combine(_value, _kind);
    }
}
