using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Net;
using System.Text.RegularExpressions;
using System.Text.Json;
using Fluid.Values;
using TimeZoneConverter;
using Fluid.Utils;
using System.Threading.Tasks;

namespace Fluid.Filters
{
    public static class MiscFilters
    {

        private static readonly Regex HtmlCaseRegex =
            new Regex(
                "(?<!^)((?<=[a-zA-Z0-9])[A-Z][a-z])|((?<=[a-z])[A-Z])",
                RegexOptions.None,
                TimeSpan.FromMilliseconds(500));

        private const string Now = "now";
        private const string Today = "today";

        public static FilterCollection WithMiscFilters(this FilterCollection filters)
        {
            filters.AddFilter("default", Default);
            filters.AddFilter("date", Date);
            filters.AddFilter("raw", Raw);
            filters.AddFilter("compact", Compact);
            filters.AddFilter("url_encode", UrlEncode);
            filters.AddFilter("url_decode", UrlDecode);
            filters.AddFilter("strip_html", StripHtml);
            filters.AddFilter("escape", Escape);
            filters.AddFilter("escape_once", EscapeOnce);
            filters.AddFilter("handle", Handleize);
            filters.AddFilter("handleize", Handleize);
            filters.AddFilter("json", Json);
            filters.AddFilter("time_zone", ChangeTimeZone);

            filters.AddFilter("format_number", FormatNumber);
            filters.AddFilter("format_string", FormatString);
            filters.AddFilter("format_date", FormatDate);

            return filters;
        }

        /// <summary>
        /// Converts from pascal/camel case to lower kebab-case.
        /// </summary>
        public static ValueTask<FluidValue> Handleize(FluidValue input, FilterArguments arguments, TemplateContext context)
        {
            return new StringValue(HtmlCaseRegex.Replace(input.ToStringValue(), "-$1$2").ToLowerInvariant());
        }

        public static ValueTask<FluidValue> Default(FluidValue input, FilterArguments arguments, TemplateContext context)
        {
            return input.Or(arguments.At(0));
        }

        public static ValueTask<FluidValue> Raw(FluidValue input, FilterArguments arguments, TemplateContext context)
        {
            var stringValue = new StringValue(input.ToStringValue(), false);

            return stringValue;
        }

        public static ValueTask<FluidValue> Compact(FluidValue input, FilterArguments arguments, TemplateContext context)
        {
            var compacted = new List<FluidValue>();
            foreach(var value in input.Enumerate())
            {
                if (!value.IsNil())
                {
                    compacted.Add(value);
                }
            }

            return new ArrayValue(compacted);
        }

        public static ValueTask<FluidValue> UrlEncode(FluidValue input, FilterArguments arguments, TemplateContext context)
        {
            return new StringValue(WebUtility.UrlEncode(input.ToStringValue()));
        }

        public static ValueTask<FluidValue> UrlDecode(FluidValue input, FilterArguments arguments, TemplateContext context)
        {
            return new StringValue(WebUtility.UrlDecode(input.ToStringValue()));
        }

        public static ValueTask<FluidValue> StripHtml(FluidValue input, FilterArguments arguments, TemplateContext context)
        {
            var html = input.ToStringValue();
            if (String.IsNullOrEmpty(html))
            {
                return StringValue.Empty;
            }

            try
            {
                var result = new char[html.Length];
                var cursor = 0;
                var inside = false;
                for (var i = 0; i < html.Length; i++)
                {
                    char current = html[i];

                    switch (current)
                    {
                        case '<':
                            inside = true;
                            continue;
                        case '>':
                            inside = false;
                            continue;
                    }

                    if (!inside)
                    {
                        result[cursor++] = current;
                    }
                }

                return new StringValue(new string(result, 0, cursor));
            }
            catch
            {
                return new StringValue(String.Empty);
            }
        }

        public static ValueTask<FluidValue> Escape(FluidValue input, FilterArguments arguments, TemplateContext context)
        {
            return new StringValue(WebUtility.HtmlEncode(input.ToStringValue()));
        }

        public static ValueTask<FluidValue> EscapeOnce(FluidValue input, FilterArguments arguments, TemplateContext context)
        {
            return new StringValue(WebUtility.HtmlEncode(WebUtility.HtmlDecode(input.ToStringValue())));
        }

        public static ValueTask<FluidValue> ChangeTimeZone(FluidValue input, FilterArguments arguments, TemplateContext context)
        {
            if (!TryGetDateTimeInput(input, context, out var value))
            {
                return NilValue.Instance;
            }

            if (arguments.At(0).IsNil())
            {
                return NilValue.Instance;
            }

            var timeZone = arguments.At(0).ToStringValue();

            if (!TZConvert.TryGetTimeZoneInfo(timeZone, out var timeZoneInfo)) return new DateTimeValue(value);

            var result = TimeZoneInfo.ConvertTime(value, timeZoneInfo);
            return new DateTimeValue(result);
        }


        public static ValueTask<FluidValue> Date(FluidValue input, FilterArguments arguments, TemplateContext context)
        {
            if (!TryGetDateTimeInput(input, context, out var value))
            {
                return NilValue.Instance;
            }

            if (arguments.At(0).IsNil())
            {
                return NilValue.Instance;
            }

            var format = arguments.At(0).ToStringValue();

            using (var sb = StringBuilderPool.GetInstance())
            {
                sb.Builder.EnsureCapacity(format.Length * 2);
                var result = sb.Builder;
                var percent = false;

                var removeLeadingZerosFlag = false;
                var useSpaceForPaddingFlag = false;
                var upperCaseFlag = false;
                var useColonsForZeeDirectiveFlag = false;

                for (var i = 0; i < format.Length; i++)
                {
                    var c = format[i];
                    if (!percent)
                    {
                        if (c == '%')
                        {
                            percent = true;
                        }
                        else
                        {
                            result.Append(c);
                        }
                    }
                    else
                    {
                        switch (c)
                        {
                            case '^': upperCaseFlag = true; continue;
                            case '-': removeLeadingZerosFlag = true; continue;
                            case '_': useSpaceForPaddingFlag = true; continue;
                            case ':': useColonsForZeeDirectiveFlag = true; continue;
                            case 'a':
                            {
                                var abbreviatedDayName = context.Options.CultureInfo.DateTimeFormat.AbbreviatedDayNames[(int) value.DayOfWeek];
                                result.Append(upperCaseFlag ? abbreviatedDayName.ToUpper() : abbreviatedDayName);
                                break;
                            }
                            case 'A':
                            {
                                var dayName = context.Options.CultureInfo.DateTimeFormat.DayNames[(int) value.DayOfWeek];
                                result.Append(upperCaseFlag ? dayName.ToUpper() : dayName);
                                break;
                            }
                            case 'b':
                            {
                                var abbreviatedMonthName = context.Options.CultureInfo.DateTimeFormat.AbbreviatedMonthNames[value.Month - 1];
                                result.Append(upperCaseFlag ? abbreviatedMonthName.ToUpper() : abbreviatedMonthName);
                                break;
                            }
                            case 'B':
                            {
                                var monthName = context.Options.CultureInfo.DateTimeFormat.MonthNames[value.Month - 1];
                                result.Append(upperCaseFlag ? monthName.ToUpper() : monthName);
                                break;
                            }
                            case 'c':
                            {
                                var f = value.ToString("F", context.Options.CultureInfo);
                                result.Append(upperCaseFlag ? f.ToUpper() : f);
                                break;
                            }
                            case 'C': result.Append(value.Year / 100); break;
                            case 'd':
                            {
                                var day = value.Day.ToString(context.Options.CultureInfo);
                                if (useSpaceForPaddingFlag)
                                {
                                    result.Append(day.PadLeft(2, ' '));
                                }
                                else if (removeLeadingZerosFlag)
                                {
                                    result.Append(day);
                                }
                                else
                                {
                                    result.Append(day.PadLeft(2, '0'));
                                }
                                break;
                            }
                            case 'D': result.Append(value.ToString("d", context.Options.CultureInfo)); break;
                            case 'e': result.Append(value.Day.ToString(context.Options.CultureInfo).PadLeft(2, ' ')); break;
                            case 'F': result.Append(value.ToString("yyyy-MM-dd", context.Options.CultureInfo)); break;
                            case 'H': result.Append(value.Hour.ToString(context.Options.CultureInfo).PadLeft(2, '0')); break;
                            case 'I': result.Append((value.Hour % 12).ToString(context.Options.CultureInfo).PadLeft(2, '0')); break;
                            case 'j': result.Append(value.DayOfYear.ToString(context.Options.CultureInfo).PadLeft(3, '0')); break;
                            case 'k': result.Append(value.Hour); break;
                            case 'l': result.Append(value.ToString("%h", context.Options.CultureInfo).PadLeft(2, ' ')); break;
                            case 'L': result.Append(value.Millisecond.ToString(context.Options.CultureInfo).PadLeft(3, '0')); break;
                            case 'm':
                            {
                                var month = value.Month.ToString(context.Options.CultureInfo);
                                if (useSpaceForPaddingFlag)
                                {
                                    result.Append(month.PadLeft(2, ' '));
                                }
                                else if (removeLeadingZerosFlag)
                                {
                                    result.Append(month);
                                }
                                else
                                {
                                    result.Append(month.PadLeft(2, '0'));
                                }
                                break;
                            }
                            case 'M': result.Append(value.Minute.ToString(context.Options.CultureInfo).PadLeft(2, '0')); break;
                            case 'p': result.Append(value.ToString("tt", context.Options.CultureInfo).ToUpper()); break;
                            case 'P': result.Append(value.ToString("tt", context.Options.CultureInfo).ToLower()); break;
                            case 'T':
                            case 'r': result.Append(value.ToString("T", context.Options.CultureInfo).ToUpper()); break;
                            case 'R': result.Append(value.ToString("t", context.Options.CultureInfo).ToUpper()); break;
                            case 's': result.Append(value.ToUnixTimeSeconds()); break;
                            case 'S': result.Append(value.Second.ToString(context.Options.CultureInfo).PadLeft(2, '0')); break;
                            case 'u': result.Append((int)value.DayOfWeek); break;
                            case 'U': result.Append(context.Options.CultureInfo.Calendar.GetWeekOfYear(value.DateTime, CalendarWeekRule.FirstDay, DayOfWeek.Sunday).ToString().PadLeft(2, '0')); break;
                            case 'v':
                            {
                                var d = value.ToString("D", context.Options.CultureInfo);
                                result.Append(upperCaseFlag ? d.ToUpper() : d);
                                break;
                            }
                            case 'V': result.Append((value.DayOfYear / 7 + 1).ToString(context.Options.CultureInfo).PadLeft(2, '0')); break;
                            case 'W': result.Append(context.Options.CultureInfo.Calendar.GetWeekOfYear(value.DateTime, CalendarWeekRule.FirstDay, DayOfWeek.Monday).ToString().PadLeft(2, '0')); break;
                            case 'y': result.Append(value.ToString("yy", context.Options.CultureInfo)); break;
                            case 'Y': result.Append(value.Year); break;
                            case 'z':
                            {
                                var zzz = value.ToString("zzz", context.Options.CultureInfo);
                                result.Append(useColonsForZeeDirectiveFlag ? zzz : zzz.Replace(":", ""));
                                break;
                            }
                            case 'Z':
                                result.Append(value.ToString("zzz", context.Options.CultureInfo));
                                break;
                            case '%': result.Append('%'); break;
                            default: result.Append('%').Append(c); break;
                        }

                        percent = false;
                        removeLeadingZerosFlag = false;
                        useSpaceForPaddingFlag = false;
                        upperCaseFlag = false;
                        useColonsForZeeDirectiveFlag = false;
                    }
                }

                return new StringValue(result.ToString());
            }

        }

        public static ValueTask<FluidValue> FormatDate(FluidValue input, FilterArguments arguments, TemplateContext context)
        {
            if (!TryGetDateTimeInput(input, context, out var value))
            {
                return NilValue.Instance;
            }

            if (arguments.At(0).IsNil())
            {
                return NilValue.Instance;
            }

            var format = arguments.At(0).ToStringValue();

            var culture = context.Options.CultureInfo;

            if (!arguments.At(1).IsNil())
            {
                culture = CultureInfo.CreateSpecificCulture(arguments.At(1).ToStringValue()) ?? context.Options.CultureInfo;
            }

            return new StringValue(value.ToString(format, culture));
        }

        private static bool TryGetDateTimeInput(FluidValue input, TemplateContext context, out DateTimeOffset result)
        {
            result = context.Options.Now();

            if (input.Type == FluidValues.String)
            {
                var stringValue = input.ToStringValue();

                if (stringValue == Now || stringValue == Today)
                {
                    return true;
                }
                else
                {
                    return DateTimeOffset.TryParse(stringValue, context.Options.CultureInfo, DateTimeStyles.AssumeUniversal, out result);
                }
            }
            else if (input.Type == FluidValues.Number)
            {
                result = DateTimeOffset.FromUnixTimeSeconds((long)input.ToNumberValue());
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

        public static ValueTask<FluidValue> Json(FluidValue input, FilterArguments arguments, TemplateContext context)
        {
            var options = new JsonSerializerOptions
            {
                WriteIndented = arguments.At(0).ToBooleanValue()
            };

            return input.Type switch
            {
                FluidValues.Array => new StringValue(JsonSerializer.Serialize(input.Enumerate().Select(o => o.ToObjectValue()), options)),
                FluidValues.Boolean => new StringValue(JsonSerializer.Serialize(input.ToBooleanValue(), options)),
                FluidValues.Nil => StringValue.Create("null"),
                FluidValues.Number => new StringValue(JsonSerializer.Serialize(input.ToNumberValue(), options)),
                FluidValues.DateTime or FluidValues.Dictionary or FluidValues.Object => new StringValue(JsonSerializer.Serialize(input.ToObjectValue(), options)),
                FluidValues.String => new StringValue(JsonSerializer.Serialize(input.ToStringValue(), options)),
                _ => throw new NotSupportedException("Unrecognized FluidValue"),
            };
        }

        public static ValueTask<FluidValue> FormatNumber(FluidValue input, FilterArguments arguments, TemplateContext context)
        {
            if (arguments.At(0).IsNil())
            {
                return NilValue.Instance;
            }

            var format = arguments.At(0).ToStringValue();

            var culture = context.Options.CultureInfo;

            if (!arguments.At(1).IsNil())
            {
                culture = CultureInfo.CreateSpecificCulture(arguments.At(1).ToStringValue()) ?? context.Options.CultureInfo;
            }

            return new StringValue(input.ToNumberValue().ToString(format, culture));
        }

        public static ValueTask<FluidValue> FormatString(FluidValue input, FilterArguments arguments, TemplateContext context)
        {
            if (input.IsNil())
            {
                return NilValue.Instance;
            }

            var format = input.ToStringValue();

            var culture = context.Options.CultureInfo;

            if (arguments.HasNamed("culture"))
            {
                culture = CultureInfo.CreateSpecificCulture(arguments["culture"].ToStringValue()) ?? context.Options.CultureInfo;
            }

            var parameters = arguments.ValuesToObjectArray();

            return new StringValue(string.Format(culture, format, parameters));
        }
    }
}
