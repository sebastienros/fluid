namespace Fluid.Values;

/// <summary>
/// What a date read by the date filters knows of its time zone, for <c>%Z</c>, like a Ruby <c>Time</c> or <c>Date</c>:
/// a UTC time is named <c>UTC</c>, a <c>Date</c> <c>+00:00</c>, and another time has the name of its time zone, the
/// context's one unless the <c>time_zone</c> filter set another, when the offset is the time zone's own at that instant.
/// </summary>
internal readonly struct DateTimeZone
{
    private const byte UtcKind = 1;
    private const byte DateKind = 2;

    private readonly byte _kind;

    private DateTimeZone(TimeZoneInfo timeZone, byte kind)
    {
        TimeZone = timeZone;
        _kind = kind;
    }

    /// <summary>A time in the time zone of the context, or with an offset.</summary>
    public static DateTimeZone Context => default;

    /// <summary>A time in UTC, like Ruby's <c>Time.utc</c>.</summary>
    public static DateTimeZone Utc => new(null, UtcKind);

    /// <summary>A date, like a Ruby <c>Date</c>.</summary>
    public static DateTimeZone Date => new(null, DateKind);

    /// <summary>A time in a time zone, like the result of the <c>time_zone</c> filter.</summary>
    public static DateTimeZone In(TimeZoneInfo timeZone) => new(timeZone, 0);

    /// <summary>The time zone of the time, or <c>null</c> for the context's one.</summary>
    public TimeZoneInfo TimeZone { get; }

    public bool IsUtc => _kind == UtcKind;

    public bool IsDate => _kind == DateKind;
}
