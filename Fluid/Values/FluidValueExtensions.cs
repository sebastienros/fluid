using System.Globalization;

namespace Fluid.Values;

public static class FluidValueExtensions
{
    private const string Now = "now";
    private const string Today = "today";

    // The K specifier is optional when used in TryParseExact, so
    // if a TZ is not specified, it will still match

    private static readonly string[] DefaultFormats = [
        "yyyy-MM-ddTHH:mm:ss.FFFK",
        "yyyy-MM-ddTHH:mm:ssK",
        "yyyy-MM-ddTHH:mmK",
        "yyyy-MM-dd",
        "yyyy-MM",
        "yyyy"
    ];

    private static readonly string[] SecondaryFormats = [
        // Formats used in DatePrototype toString methods
        "ddd MMM dd yyyy HH:mm:ss 'GMT'K",
        "ddd MMM dd yyyy",
        "HH:mm:ss 'GMT'K",

        // standard formats
        "yyyy-M-dTH:m:s.FFFK",
        "yyyy/M/dTH:m:s.FFFK",
        "yyyy-M-dTH:m:sK",
        "yyyy/M/dTH:m:sK",
        "yyyy-M-dTH:mK",
        "yyyy/M/dTH:mK",
        "yyyy-M-d H:m:s.FFFK",
        "yyyy/M/d H:m:s.FFFK",
        "yyyy-M-d H:m:sK",
        "yyyy/M/d H:m:sK",
        "yyyy-M-d H:mK",
        "yyyy/M/d H:mK",
        "yyyy-M-dK",
        "yyyy/M/dK",
        "yyyy-MK",
        "yyyy/MK",
        "yyyyK",
        "THH:mm:ss.FFFK",
        "THH:mm:ssK",
        "THH:mmK",
        "THHK"
    ];

    public static bool TryGetDateTimeInput(this FluidValue input, TemplateContext context, out DateTimeOffset result)
    {
        return TryGetDateTimeInput(input, context, out result, out _);
    }

    /// <summary>
    /// Reads a date like <see cref="TryGetDateTimeInput(FluidValue, TemplateContext, out DateTimeOffset)"/>, and what it
    /// knows of its time zone, which <c>%Z</c> shows like Ruby.
    /// </summary>
    internal static bool TryGetDateTimeInput(this FluidValue input, TemplateContext context, out DateTimeOffset result, out DateTimeZone zone)
    {
        zone = DateTimeZone.Context;

        if (input.Type == FluidValues.String)
        {
            var stringValue = input.ToStringValue(context);

            // Like Liquid, which downcases the string.
            if (string.Equals(stringValue, Now, StringComparison.OrdinalIgnoreCase) || string.Equals(stringValue, Today, StringComparison.OrdinalIgnoreCase))
            {
                result = context.Now();

                return true;
            }
            // Try to parse as Unix timestamp (seconds since epoch)
            // Golden Liquid only supports positive Unix timestamps
            else if (long.TryParse(stringValue, NumberStyles.None, CultureInfo.InvariantCulture, out var timestamp))
            {
                try
                {
                    var dateTime = DateTimeOffset.FromUnixTimeSeconds(timestamp);
                    result = dateTime.ToOffset(context.TimeZone.GetUtcOffset(dateTime));
                    return true;
                }
                catch
                {
                    result = default;
                    return false;
                }
            }

            // Like Liquid, a string of digits is always a timestamp: one that is too large isn't read as a date.
            if (IsDigits(stringValue))
            {
                result = default;
                return false;
            }

            // A specific culture reads dates in its formats first, like 08/01/2017 as August 1 in en-US and January 8 in fr-FR.
            if (context.CultureInfo.Name.Length > 0 && TryParseWithCulture(stringValue, context, out result))
            {
                // Like Ruby, a time in Z or UTC is a UTC time.
                if (result.Offset == TimeSpan.Zero && HasUtcDesignator(stringValue))
                {
                    zone = DateTimeZone.Utc;
                }

                return true;
            }

            // Otherwise like Liquid, which uses Ruby's Time.parse.
            if (!RubyTimeParser.TryParse(stringValue, context, out result, out var utc))
            {
                return false;
            }

            if (utc)
            {
                zone = DateTimeZone.Utc;
            }

            return true;
        }
        else if (input.Type == FluidValues.Number)
        {
            try
            {
                // Out of range for a decimal or a date, the input isn't a date.
                var seconds = input.ToNumberValue(context);

                // Like Liquid, only an integer is a timestamp: Ruby's Time.at isn't called with a Float.
                if (NumberValue.GetScale(seconds) != 0)
                {
                    result = default;
                    return false;
                }

                var dateTime = DateTimeOffset.FromUnixTimeSeconds((long)seconds);
                result = dateTime.ToOffset(context.TimeZone.GetUtcOffset(dateTime));
                return true;
            }
            catch
            {
                result = default;
                return false;
            }
        }
        else if (input is DateTimeValue dateTimeValue)
        {
            result = dateTimeValue.Value;
            zone = dateTimeValue.Zone;
        }
        else if (input is PlainDateTimeValue plain)
        {
            if (plain.IsDate)
            {
                zone = DateTimeZone.Date;
            }

            return plain.TryGetDateTimeOffset(context, out result);
        }
        else if (input is TimeSpanValue timeSpan)
        {
            return timeSpan.TryGetDateTimeOffset(context, out result);
        }
        else
        {
            switch (input.ToObjectValue(context))
            {
                case DateTime dateTime:
                    // A date and time without time zone is in the one of the context, not the one of the machine.
                    result = dateTime.Kind == DateTimeKind.Unspecified ? InTimeZone(dateTime, context) : dateTime;

                    if (dateTime.Kind == DateTimeKind.Utc)
                    {
                        zone = DateTimeZone.Utc;
                    }

                    break;

                case DateTimeOffset dateTimeOffset:
                    result = dateTimeOffset;
                    break;

                default:
                    result = default;
                    return false;
            }
        }

        return true;
    }

    // A string that ends with Z or UTC, a zone Ruby reads as UTC.
    private static bool HasUtcDesignator(string value)
    {
        var trimmed = value.TrimEnd();
        return trimmed.EndsWith("Z", StringComparison.OrdinalIgnoreCase) || trimmed.EndsWith("UTC", StringComparison.OrdinalIgnoreCase);
    }

    private static bool IsDigits(string value)
    {
        foreach (var c in value)
        {
            if (c is < '0' or > '9')
            {
                return false;
            }
        }

        return value.Length > 0;
    }

    private static bool TryParseWithCulture(string stringValue, TemplateContext context, out DateTimeOffset result)
    {
        // DateTimeOffset reads the offset if there is one, DateTime's Kind tells if there was one.
        bool timeZoneProvided;

        if (DateTimeOffset.TryParseExact(stringValue, DefaultFormats, context.CultureInfo, DateTimeStyles.AssumeUniversal, out result))
        {
            timeZoneProvided = DateTime.TryParseExact(stringValue, DefaultFormats, context.CultureInfo, DateTimeStyles.None, out var timeZoneDateTime) && timeZoneDateTime.Kind != DateTimeKind.Unspecified;
        }
        else if (DateTimeOffset.TryParseExact(stringValue, SecondaryFormats, context.CultureInfo, DateTimeStyles.AssumeUniversal, out result))
        {
            timeZoneProvided = DateTime.TryParseExact(stringValue, SecondaryFormats, context.CultureInfo, DateTimeStyles.None, out var timeZoneDateTime) && timeZoneDateTime.Kind != DateTimeKind.Unspecified;
        }
        else if (DateTimeOffset.TryParse(stringValue, context.CultureInfo, DateTimeStyles.AssumeUniversal, out result))
        {
            timeZoneProvided = DateTime.TryParse(stringValue, context.CultureInfo, DateTimeStyles.None, out var timeZoneDateTime) && timeZoneDateTime.Kind != DateTimeKind.Unspecified;
        }
        else
        {
            return false;
        }

        if (!timeZoneProvided)
        {
            result = InTimeZone(result.DateTime, context);
        }

        return true;
    }

    // A date without offset is in the time zone of the context, with the offset at this date and time
    // since it can vary with daylight saving time.
    private static DateTimeOffset InTimeZone(DateTime dateTime, TemplateContext context)
    {
        return new DateTimeOffset(dateTime, context.TimeZone.GetUtcOffset(dateTime));
    }

    public static FluidValue Or(this FluidValue self, FluidValue other)
    {
        if (self.IsNil())
        {
            return other;
        }

        return self;
    }

    public static IEnumerable<T> ToEnumerable<T>(this IAsyncEnumerable<T> asyncEnumerable)
    {
        var enumerator = asyncEnumerable.GetAsyncEnumerator();
        try
        {
            var moveNextTask = enumerator.MoveNextAsync();
            while (moveNextTask.IsCompleted ? moveNextTask.Result : moveNextTask.AsTask().GetAwaiter().GetResult())
            {
                yield return enumerator.Current;
                moveNextTask = enumerator.MoveNextAsync();
            }
        }
        finally
        {
            var disposeTask = enumerator.DisposeAsync();
            if (!disposeTask.IsCompleted)
            {
                disposeTask.AsTask().GetAwaiter().GetResult();
            }
        }
    }
}
