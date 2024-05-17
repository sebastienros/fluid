using System.Globalization;

namespace Fluid.Values
{
    public static class FluidValueExtensions
    {
        private const string Now = "now";
        private const string Today = "today";

        // The K specifier is optional when used in TryParseExact, so
        // if a TZ is not specified, it will still match

        private static readonly string[] DefaultFormats = {
            "yyyy-MM-ddTHH:mm:ss.FFFK",
            "yyyy-MM-ddTHH:mm:ssK",
            "yyyy-MM-ddTHH:mmK",
            "yyyy-MM-dd",
            "yyyy-MM",
            "yyyy"
        };

        private static readonly string[] SecondaryFormats = {
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
        };

        public static bool TryGetDateTimeInput(this FluidValue input, TemplateContext context, out DateTimeOffset result)
        {
            result = context.Now();

            if (input.Type == FluidValues.String)
            {
                var timeZoneProvided = false;

                var stringValue = input.ToStringValue();

                if (stringValue == Now || stringValue == Today)
                {
                    return true;
                }
                else
                {
                    var success = true;

                    // Use DateTimeOffset.Parse to extract the TZ if it's specified.
                    // We then verify if a TZ was set in the source string by using DateTime.Parse's Kind which will return Unspecified if not set.

                    if (!DateTimeOffset.TryParseExact(stringValue, DefaultFormats, context.CultureInfo, DateTimeStyles.AssumeUniversal, out result))
                    {
                        if (!DateTimeOffset.TryParseExact(stringValue, SecondaryFormats, context.CultureInfo, DateTimeStyles.AssumeUniversal, out result))
                        {
                            if (!DateTimeOffset.TryParse(stringValue, context.CultureInfo, DateTimeStyles.AssumeUniversal, out result))
                            {
                                if (!DateTimeOffset.TryParse(stringValue, CultureInfo.InvariantCulture, DateTimeStyles.AssumeUniversal, out result))
                                {
                                    success = false;
                                }
                                else
                                {
                                    timeZoneProvided = DateTime.TryParse(stringValue, CultureInfo.InvariantCulture, DateTimeStyles.None, out var timeZoneDateTime) && timeZoneDateTime.Kind != DateTimeKind.Unspecified;
                                }
                            }
                            else
                            {
                                timeZoneProvided = DateTime.TryParse(stringValue, context.CultureInfo, DateTimeStyles.None, out var timeZoneDateTime) && timeZoneDateTime.Kind != DateTimeKind.Unspecified;
                            }
                        }
                        else
                        {
                            timeZoneProvided = DateTime.TryParseExact(stringValue, SecondaryFormats, context.CultureInfo, DateTimeStyles.None, out var timeZoneDateTime) && timeZoneDateTime.Kind != DateTimeKind.Unspecified;
                        }
                    }
                    else
                    {
                        timeZoneProvided = DateTime.TryParseExact(stringValue, DefaultFormats, context.CultureInfo, DateTimeStyles.None, out var timeZoneDateTime) && timeZoneDateTime.Kind != DateTimeKind.Unspecified;
                    }

                    if (success)
                    {
                        // If no timezone is specified in the source string, only use the date time part of the result
                        if (!timeZoneProvided)
                        {
                            // A timezone is represented as a UTC offset, but this can vary based on daylight saving times.
                            // Hence we don't use context.TimeZone.BaseUtcOffset which is fixed, but TimeZone.GetUtcOffset
                            // to get the actual timezone offset at the moment of the parsed date and time

                            var dateTime = result.DateTime;
                            var offset = context.TimeZone.GetUtcOffset(dateTime);

                            result = new DateTimeOffset(dateTime, offset);
                        }
                    }

                    return success;
                }
            }
            else if (input.Type == FluidValues.Number)
            {
                var milliseconds = input.ToNumberValue() * 1000;
                var dateTime = DateTimeOffset.FromUnixTimeMilliseconds((long)milliseconds);
                result = dateTime.ToOffset(context.TimeZone.GetUtcOffset(dateTime));
            }
            else if (input.Type == FluidValues.DateTime)
            {
                result = (DateTimeOffset)input.ToObjectValue();
            }
            else
            {
                switch (input.ToObjectValue())
                {
                    case DateTime dateTime:
                        result = dateTime;
                        break;

                    case DateTimeOffset dateTimeOffset:
                        result = dateTimeOffset;
                        break;

                    default:
                        return false;
                }
            }

            return true;
        }

        public static FluidValue Or(this FluidValue self, FluidValue other)
        {
            if (self.IsNil())
            {
                return other;
            }

            return self;
        }
    }
}
