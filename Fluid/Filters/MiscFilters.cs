using System;
using System.Collections.Generic;
using System.Net;
using System.Text;
using Fluid.Values;

namespace Fluid.Filters
{
    public static class MiscFilters
    {
        public static FilterCollection WithMiscFilters(this FilterCollection filters)
        {
            filters.AddFilter("default", Default);
            filters.AddFilter("date", Date);
            filters.AddFilter("format_date", FormatDate);
            filters.AddFilter("raw", Raw);
            filters.AddFilter("compact", Compact);
            filters.AddFilter("url_encode", UrlEncode);
            filters.AddFilter("url_decode", UrlDecode);
            filters.AddFilter("strip_html", StripHtml);
            filters.AddFilter("escape", StripHtml);
            filters.AddFilter("escape_once", StripHtml);

            return filters;
        }

        public static FluidValue Default(FluidValue input, FilterArguments arguments, TemplateContext context)
        {
            return input.Or(arguments.At(0));
        }

        public static FluidValue Raw(FluidValue input, FilterArguments arguments, TemplateContext context)
        {
            var stringValue = new StringValue(input.ToStringValue(), false);

            return stringValue;
        }

        public static FluidValue Compact(FluidValue input, FilterArguments arguments, TemplateContext context)
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

        public static FluidValue UrlEncode(FluidValue input, FilterArguments arguments, TemplateContext context)
        {
            return new StringValue(WebUtility.UrlEncode(input.ToStringValue()));
        }

        public static FluidValue UrlDecode(FluidValue input, FilterArguments arguments, TemplateContext context)
        {
            return new StringValue(WebUtility.UrlDecode(input.ToStringValue()));
        }

        public static FluidValue StripHtml(FluidValue input, FilterArguments arguments, TemplateContext context)
        {
            var html = input.ToStringValue();
            
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

        public static FluidValue Escape(FluidValue input, FilterArguments arguments, TemplateContext context)
        {
            return new StringValue(WebUtility.HtmlEncode(input.ToStringValue()));
        }

        public static FluidValue EscapeOnce(FluidValue input, FilterArguments arguments, TemplateContext context)
        {
            return new StringValue(WebUtility.HtmlEncode(WebUtility.HtmlDecode(input.ToStringValue())));
        }

        public static FluidValue Date(FluidValue input, FilterArguments arguments, TemplateContext context)
        {
            DateTimeOffset value;

            if (input.Type == FluidValues.String && input.ToStringValue() == "now")
            {
                value = context.Now();
            }
            else
            {
                switch (input.ToObjectValue())
                {
                    case DateTime dateTime:
                        value = dateTime;
                        break;

                    case DateTimeOffset dateTimeOffset:
                        value = dateTimeOffset;
                        break;

                    default:
                        return NilValue.Instance;
                }
            }

            if (arguments.At(0).IsNil())
            {
                return NilValue.Instance;
            }

            var format = arguments.At(0).ToStringValue();

            var result = new StringBuilder(format.Length * 2);
            var percent = false;

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
                        case 'a': result.Append(context.CultureInfo.DateTimeFormat.AbbreviatedDayNames[(int)value.DayOfWeek]); break;
                        case 'A': result.Append(context.CultureInfo.DateTimeFormat.DayNames[(int)value.DayOfWeek]); break;
                        case 'b': result.Append(context.CultureInfo.DateTimeFormat.AbbreviatedMonthNames[value.Month - 1]); break;
                        case 'B': result.Append(context.CultureInfo.DateTimeFormat.MonthNames[value.Month - 1]); break;
                        case 'c': result.Append(value.ToString("ddd MMM dd HH:mm:ss yyyy", context.CultureInfo)); break;
                        case 'C': result.Append(value.Year / 100); break;
                        case 'd': result.Append(value.Day.ToString(context.CultureInfo).PadLeft(2, '0')); break;
                        case 'D':
                            result
                                .Append(value.Month.ToString(context.CultureInfo).PadLeft(2, '0'))
                                .Append('/').Append(value.Day.ToString(context.CultureInfo).PadLeft(2, '0'))
                                .Append('/').Append((value.Year % 100).ToString(context.CultureInfo).PadLeft(2, '0'));
                            break;
                        case 'e': result.Append(value.Day.ToString(context.CultureInfo).PadLeft(2, ' ')); break;
                        case 'F': result.Append(value.ToString("yyyy-MM-dd", context.CultureInfo)); break;
                        case 'H': result.Append(value.Hour.ToString(context.CultureInfo).PadLeft(2, '0')); break;
                        case 'I': result.Append((value.Hour % 12).ToString(context.CultureInfo).PadLeft(2, '0')); break;
                        case 'j': result.Append(value.DayOfYear.ToString(context.CultureInfo).PadLeft(3, '0')); break;
                        case 'k': result.Append(value.Hour); break;
                        case 'l': result.Append((value.Hour % 12).ToString(context.CultureInfo).PadLeft(2, ' ')); break;
                        case 'L': result.Append(value.Millisecond.ToString(context.CultureInfo).PadLeft(3, '0')); break;
                        case 'm': result.Append(value.Month.ToString(context.CultureInfo).PadLeft(2, '0')); break;
                        case 'M': result.Append(value.Minute.ToString(context.CultureInfo).PadLeft(2, '0')); break;
                        case 'p': result.Append(value.ToString("tt", context.CultureInfo).ToUpper()); break;
                        case 'P': result.Append(value.ToString("tt", context.CultureInfo).ToLower()); break;
                        case 'r':
                            result
                                .Append((value.Hour % 12).ToString(context.CultureInfo).PadLeft(2, '0')).Append(":")
                                .Append(value.Minute.ToString(context.CultureInfo).PadLeft(2, '0')).Append(":")
                                .Append(value.Second.ToString(context.CultureInfo).PadLeft(2, '0')).Append(" ")
                                .Append(value.ToString("tt", context.CultureInfo).ToUpper())
                                ;
                            break;
                        case 'R':
                            result
                              .Append(value.Hour.ToString(context.CultureInfo).PadLeft(2, '0')).Append(":")
                              .Append(value.Minute.ToString(context.CultureInfo).PadLeft(2, '0'));
                            break;
                        case 's': result.Append(value.ToUnixTimeSeconds()); break;
                        case 'S': result.Append(value.Second.ToString(context.CultureInfo).PadLeft(2, '0')); break;
                        case 'T':
                            result
                              .Append(value.Hour.ToString(context.CultureInfo).PadLeft(2, '0')).Append(":")
                              .Append(value.Minute.ToString(context.CultureInfo).PadLeft(2, '0')).Append(":")
                              .Append(value.Second.ToString(context.CultureInfo).PadLeft(2, '0'))
                              ;
                            break;
                        case 'u': result.Append((int)value.DayOfWeek); break;
                        case 'U': result.Append(context.CultureInfo.Calendar.GetWeekOfYear(value.DateTime, System.Globalization.CalendarWeekRule.FirstDay, DayOfWeek.Sunday).ToString().PadLeft(2, '0')); break;
                        case 'v':
                            result
                                .Append(value.Day.ToString(context.CultureInfo).PadLeft(2, ' ')).Append("-")
                                .Append(context.CultureInfo.DateTimeFormat.AbbreviatedMonthNames[value.Month - 1]).Append("-")
                                .Append(value.Year)
                                ;
                            break;
                        case 'V': result.Append((value.DayOfYear / 7 + 1).ToString(context.CultureInfo).PadLeft(2, '0')); break;
                        case 'W': result.Append(context.CultureInfo.Calendar.GetWeekOfYear(value.DateTime, System.Globalization.CalendarWeekRule.FirstDay, DayOfWeek.Monday).ToString().PadLeft(2, '0')); break;
                        case 'y': result.Append(value.ToString("yy", context.CultureInfo)); break;
                        case 'Y': result.Append(value.Year); break;
                        case 'z': result.Append(value.ToString("zzz", context.CultureInfo)); break;
                        case 'Z': goto default; // unsupported
                        case '%': result.Append('%'); break;
                        default: result.Append('%').Append(c); break;
                    }

                    percent = false;
                }
            }

            return new StringValue(result.ToString());
        }

        public static FluidValue FormatDate(FluidValue input, FilterArguments arguments, TemplateContext context)
        {
            DateTimeOffset value;

            if (input.Type == FluidValues.String && input.ToStringValue() == "now")
            {
                value = context.Now();
            }
            else
            {
                switch (input.ToObjectValue())
                {
                    case DateTime dateTime:
                        value = dateTime;
                        break;

                    case DateTimeOffset dateTimeOffset:
                        value = dateTimeOffset;
                        break;

                    default:
                        return NilValue.Instance;
                }
            }

            if (arguments.At(0).IsNil())
            {
                return NilValue.Instance;
            }

            var format = arguments.At(0).ToStringValue();

            return new StringValue(value.ToString(format, context.CultureInfo));
        }
    }
}
