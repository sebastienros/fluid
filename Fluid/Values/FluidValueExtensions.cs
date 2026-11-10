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
                return true;
            }

            // Otherwise like Liquid, which uses Ruby's Time.parse.
            return RubyTimeParser.TryParse(stringValue, context, out result);
        }
        else if (input.Type == FluidValues.Number)
        {
            try
            {
                // Out of range for a decimal or a date, the input isn't a date.
                var milliseconds = input.ToNumberValue(context) * 1000;
                var dateTime = DateTimeOffset.FromUnixTimeMilliseconds((long)milliseconds);
                result = dateTime.ToOffset(context.TimeZone.GetUtcOffset(dateTime));
                return true;
            }
            catch
            {
                result = default;
                return false;
            }
        }
        else if (input.Type == FluidValues.DateTime)
        {
            result = (DateTimeOffset)input.ToObjectValue(context);
        }
        else
        {
            switch (input.ToObjectValue(context))
            {
                case DateTime dateTime:
                    result = dateTime;
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
