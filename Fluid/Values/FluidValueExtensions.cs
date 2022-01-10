using System;
using System.Globalization;

namespace Fluid.Values
{
    public static class FluidValueExtensions
    {
        private const string Now = "now";
        private const string Today = "today";

        private static readonly string[] DefaultFormats = {
            "yyyy-MM-ddTHH:mm:ss.FFF",
            "yyyy-MM-ddTHH:mm:ss",
            "yyyy-MM-ddTHH:mm",
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
                var stringValue = input.ToStringValue();

                if (stringValue == Now || stringValue == Today)
                {
                    return true;
                }
                else
                {
                    var success = true;

                    if (!DateTime.TryParseExact(stringValue, DefaultFormats, context.CultureInfo, DateTimeStyles.None, out var dateTime))
                    {
                        if (!DateTime.TryParseExact(stringValue, SecondaryFormats, context.CultureInfo, DateTimeStyles.None, out dateTime))
                        {
                            if (!DateTime.TryParse(stringValue, context.CultureInfo, DateTimeStyles.None, out dateTime))
                            {
                                if (!DateTime.TryParse(stringValue, CultureInfo.InvariantCulture, DateTimeStyles.None, out dateTime))
                                {
                                    success = false;
                                }
                            }
                        }
                    }

                    // If no timezone is specified, assume local using the configured timezone
                    if (success)
                    {
                        if (dateTime.Kind == DateTimeKind.Unspecified)
                        {
                            result = new DateTimeOffset(dateTime, context.TimeZone.GetUtcOffset(dateTime));
                        }
                        else
                        {
                            result = new DateTimeOffset(dateTime);
                        }
                    }

                    return success;
                }
            }
            else if (input.Type == FluidValues.Number)
            {
                var dateTime = DateTimeOffset.FromUnixTimeSeconds((long)input.ToNumberValue());
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
