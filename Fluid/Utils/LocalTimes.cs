namespace Fluid.Utils;

/// <summary>
/// Reads a date and time without offset, a clock reading, in a time zone, like Ruby's <c>Time.local</c>.
/// </summary>
internal static class LocalTimes
{
    /// <summary>
    /// Returns the moment the clock of <paramref name="timeZone"/> shows <paramref name="dateTime"/>, with the offset the
    /// time zone has at this moment, which can vary with daylight saving time.
    /// </summary>
    /// <remarks>
    /// When the clocks move forward, the local times they skip don't exist. Like Ruby, such a time is read at the offset
    /// before the gap, and is shown at the offset after it: 02:30 in New York on 2026-03-08 is 03:30 -04:00 (07:30 UTC).
    /// A time that exists twice, when the clocks move back, is read at the standard offset, like Ruby.
    /// </remarks>
    /// <exception cref="ArgumentOutOfRangeException">The moment is before year 1 or after year 9999 in UTC.</exception>
    public static DateTimeOffset ToDateTimeOffset(DateTime dateTime, TimeZoneInfo timeZone)
    {
        var offset = timeZone.GetUtcOffset(dateTime);
        var result = new DateTimeOffset(dateTime, offset);

        // The time exists when the zone has this offset at this instant. IsInvalidTime would tell too, but .NET 10
        // doesn't see some gaps, like the day Samoa skipped (2011-12-30), and it costs a lookup as well.
        var first = timeZone.GetUtcOffset(result);

        if (first == offset)
        {
            return result;
        }

        // The offset of a time in a gap is the standard offset, which isn't always the one before the gap: Samoa
        // moved from -10:00 to +14:00, its standard offset. The offsets around the gap are the zone's offsets at two
        // instants: read at the offset after the gap, the time is an instant before the transition, which has the
        // offset before the gap, and read at the offset before, it is an instant after, with the offset after.
        var second = timeZone.GetUtcOffset(new DateTimeOffset(dateTime, first));

        // The clocks move forward over a gap, so the offset before it is the smaller one.
        var instant = new DateTimeOffset(dateTime, first < second ? first : second);

        return instant.ToOffset(timeZone.GetUtcOffset(instant));
    }
}
