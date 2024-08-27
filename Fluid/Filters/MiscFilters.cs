using Fluid.Values;
using System.Buffers;
using System.Globalization;
using System.Net;
using System.Reflection;
using System.Text;
using System.Text.Json;
using TimeZoneConverter;

namespace Fluid.Filters
{
    public static class MiscFilters
    {
        private const char KebabCaseSeparator = '-';

        public static FilterCollection WithMiscFilters(this FilterCollection filters)
        {
            filters.AddFilter("default", Default);
            filters.AddFilter("date", Date);
            filters.AddFilter("raw", Raw);
            filters.AddFilter("compact", Compact);
            filters.AddFilter("url_encode", UrlEncode);
            filters.AddFilter("url_decode", UrlDecode);
            filters.AddFilter("base64_encode", Base64Encode);
            filters.AddFilter("base64_decode", Base64Decode);
            filters.AddFilter("base64_url_safe_encode", Base64UrlSafeEncode);
            filters.AddFilter("base64_url_safe_decode", Base64UrlSafeDecode);
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

            filters.AddFilter("md5", MD5);
            filters.AddFilter("sha1", Sha1);
            filters.AddFilter("sha256", Sha256);

            return filters;
        }

        /// <summary>
        /// Converts from pascal/camel case to lower kebab-case.
        /// </summary>
        public static ValueTask<FluidValue> Handleize(FluidValue input, FilterArguments arguments, TemplateContext context)
        {
            var value = input.ToStringValue();
            var result = new StringBuilder();
            var lastIndex = value.Length - 1;

            for (int i = 0; i < value.Length; i++)
            {
                var currentChar = value[i];
                var lookAheadChar = i == lastIndex
                    ? '\0'
                    : value[i + 1];

                if (!char.IsLetterOrDigit(currentChar))
                {
                    continue;
                }

                if (i == 0 || i == lastIndex)
                {
                    result.Append(currentChar);

                    continue;
                }

                if (IsCapitalLetter(lookAheadChar))
                {
                    if (IsCapitalLetter(currentChar))
                    {
                        result.Append(currentChar);
                    }
                    else
                    {
                        if (IsCapitalLetter(lookAheadChar) && char.IsDigit(currentChar))
                        {
                            result.Append(currentChar);
                        }
                        else
                        {
                            result
                                .Append(currentChar)
                                .Append(KebabCaseSeparator);
                        }
                    }
                }
                else
                {
                    if (IsCapitalLetter(currentChar))
                    {
                        if (result[result.Length - 1] != KebabCaseSeparator && !char.IsDigit(lookAheadChar))
                        {
                            result.Append(KebabCaseSeparator);
                        }
                    }

                    result.Append(currentChar);
                }
            }

            static bool IsCapitalLetter(char c) => c >= 'A' && c <= 'Z';

            return new StringValue(result.ToString().ToLowerInvariant());
        }

        public static ValueTask<FluidValue> Default(FluidValue input, FilterArguments arguments, TemplateContext context)
        {
            var falseCheck = arguments.HasNamed("allow_false") && arguments["allow_false"] == BooleanValue.True;

            if (falseCheck)
            {
                if (input.IsNil() || EmptyValue.Instance.Equals(input))
                {
                    return arguments.At(0);
                }
            }
            else
            {
                if (input.IsNil() || input == BooleanValue.False || EmptyValue.Instance.Equals(input))
                {
                    return arguments.At(0);
                }
            }

            return input;
        }

        public static ValueTask<FluidValue> Raw(FluidValue input, FilterArguments arguments, TemplateContext context)
        {
            var stringValue = new StringValue(input.ToStringValue(), false);

            return stringValue;
        }

        public static ValueTask<FluidValue> Compact(FluidValue input, FilterArguments arguments, TemplateContext context)
        {
            var compacted = new List<FluidValue>();
            foreach (var value in input.Enumerate(context))
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

        public static ValueTask<FluidValue> Base64Encode(FluidValue input, FilterArguments arguments, TemplateContext context)
        {
            var value = input.ToStringValue();

            return String.IsNullOrEmpty(value)
                ? StringValue.Empty
                : new StringValue(Convert.ToBase64String(Encoding.UTF8.GetBytes(value)));
        }

        public static ValueTask<FluidValue> Base64Decode(FluidValue input, FilterArguments arguments, TemplateContext context)
        {
            var value = input.ToStringValue();

            return String.IsNullOrEmpty(value)
                ? StringValue.Empty
                : new StringValue(Encoding.UTF8.GetString(Convert.FromBase64String(value)));
        }

        public static ValueTask<FluidValue> Base64UrlSafeEncode(FluidValue input, FilterArguments arguments, TemplateContext context)
        {
            var value = input.ToStringValue();
            if (String.IsNullOrEmpty(value))
            {
                return StringValue.Empty;
            }
            else
            {
                var encodedBase64StringBuilder = new StringBuilder(Convert.ToBase64String(Encoding.UTF8.GetBytes(value)));

                encodedBase64StringBuilder.Replace('+', '-');
                encodedBase64StringBuilder.Replace('/', '_');

                if (encodedBase64StringBuilder[^1] == '=')
                {
                    if (encodedBase64StringBuilder[^2] == '=')
                    {
                        encodedBase64StringBuilder.Length -= 2;
                    }
                    else
                    {
                        encodedBase64StringBuilder.Length--;
                    }
                }

                return new StringValue(encodedBase64StringBuilder.ToString());
            }
        }

        public static ValueTask<FluidValue> Base64UrlSafeDecode(FluidValue input, FilterArguments arguments, TemplateContext context)
        {
            var value = input.ToStringValue();
            if (String.IsNullOrEmpty(value))
            {
                return StringValue.Empty;
            }
            else
            {
                var paddingCharsToAdd = (value.Length % 4) switch
                {
                    0 => 0,
                    2 => 2,
                    3 => 1,
                    _ => -1
                };

                if (paddingCharsToAdd == -1)
                {
                    return StringValue.Empty;
                }

                var encodedBase64StringBuilder = new StringBuilder(value);
                encodedBase64StringBuilder.Replace('-', '+');
                encodedBase64StringBuilder.Replace('_', '/');

                // Add the padding characters back.
                for (; paddingCharsToAdd > 0; paddingCharsToAdd--)
                {
                    encodedBase64StringBuilder.Append('=');
                }

                try
                {
                    var decodedBase64 = Encoding.UTF8.GetString(Convert.FromBase64String(encodedBase64StringBuilder.ToString()));
                    return new StringValue(decodedBase64);
                }
                catch
                {
                    return StringValue.Empty;
                }
            }
        }

        public static ValueTask<FluidValue> StripHtml(FluidValue input, FilterArguments arguments, TemplateContext context)
        {
            var html = input.ToStringValue();
            if (String.IsNullOrEmpty(html))
            {
                return StringValue.Empty;
            }

            var result = ArrayPool<char>.Shared.Rent(html.Length);

            try
            {
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
            finally
            {
                ArrayPool<char>.Shared.Return(result);
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
            if (!input.TryGetDateTimeInput(context, out var value))
            {
                return NilValue.Instance;
            }

            if (arguments.At(0).IsNil())
            {
                return NilValue.Instance;
            }

            var timeZone = arguments.At(0).ToStringValue();

            TimeZoneInfo timeZoneInfo;

            if (String.Equals(timeZone, "local", StringComparison.OrdinalIgnoreCase))
            {
                timeZoneInfo = context.TimeZone;
            }
            else if (!TZConvert.TryGetTimeZoneInfo(timeZone, out timeZoneInfo))
            {
                return new DateTimeValue(value);
            }

            var result = TimeZoneInfo.ConvertTime(value, timeZoneInfo);
            return new DateTimeValue(result);
        }

        // https://docs.ruby-lang.org/en/master/strftime_formatting_rdoc.html
        public static ValueTask<FluidValue> Date(FluidValue input, FilterArguments arguments, TemplateContext context)
        {
            if (!input.TryGetDateTimeInput(context, out var value))
            {
                return NilValue.Instance;
            }

            if (arguments.At(0).IsNil())
            {
                return new DateTimeValue(value);
            }

            var format = arguments.At(0).ToStringValue();

            var result = new StringBuilder(64);

            ForStrf(value, format, result);

            return new StringValue(result.ToString());

            void ForStrf(DateTimeOffset value, string format, StringBuilder result)
            {
                var percent = false;

                var removeLeadingZerosFlag = false;
                var useSpaceForPaddingFlag = false;
                var upperCaseFlag = false;
                var useColonsForZeeDirectiveFlag = false;
                int? width = null;

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
                        // Zero or more flags (each is a character).
                        switch (c)
                        {
                            case '^': upperCaseFlag = true; continue;
                            case '-': removeLeadingZerosFlag = true; continue;
                            case '_': useSpaceForPaddingFlag = true; continue;
                            case ':': useColonsForZeeDirectiveFlag = true; continue;
                            default: break;
                        }

                        // An optional width specifier (an integer).

                        if (char.IsDigit(c))
                        {
                            width ??= 0;
                            width = width * 10 + (c - '0');
                            continue;
                        }

                        switch (c)
                        {
                            case 'a':
                                string AbbreviatedDayName() => context.CultureInfo.DateTimeFormat.AbbreviatedDayNames[(int)value.DayOfWeek];

                                var abbreviatedDayName = AbbreviatedDayName();
                                result.Append(upperCaseFlag ? abbreviatedDayName.ToUpperInvariant() : abbreviatedDayName);
                                break;
                            case 'A':
                                {
                                    var dayName = context.CultureInfo.DateTimeFormat.DayNames[(int)value.DayOfWeek];
                                    result.Append(upperCaseFlag ? dayName.ToUpperInvariant() : dayName);
                                    break;
                                }
                            case 'b':
                                var abbreviatedMonthName = context.CultureInfo.DateTimeFormat.AbbreviatedMonthNames[value.Month - 1];
                                result.Append(upperCaseFlag ? abbreviatedMonthName.ToUpperInvariant() : abbreviatedMonthName);
                                break;
                            case 'B':
                                {
                                    var monthName = context.CultureInfo.DateTimeFormat.MonthNames[value.Month - 1];
                                    result.Append(upperCaseFlag ? monthName.ToUpperInvariant() : monthName);
                                    break;
                                }
                            case 'c':
                                {
                                    // c is defined as "%a %b %e %T %Y" but it's also supposed to be locale aware, so we are using the 
                                    // C# standard format instead
                                    result.Append(upperCaseFlag ? value.ToString("F", context.CultureInfo).ToUpperInvariant() : value.ToString("F", context.CultureInfo));
                                    break;
                                }
                            case 'C': result.Append(Format(value.Year / 100, 2)); break;
                            case 'd': result.Append(Format(value.Day, 2)); break;
                            case 'D':
                                {
                                    var sb = new StringBuilder();
                                    ForStrf(value, "%m/%d/%y", sb);
                                    result.Append(upperCaseFlag ? sb.ToString().ToUpperInvariant() : sb.ToString());
                                    break;
                                }
                            case 'e':
                                useSpaceForPaddingFlag = true; result.Append(Format(value.Day, 2));
                                break;
                            case 'F':
                                {
                                    var sb = new StringBuilder();
                                    ForStrf(value, "%Y-%m-%d", sb);
                                    result.Append(upperCaseFlag ? sb.ToString().ToUpperInvariant() : sb.ToString());
                                    break;
                                }
                            case 'g':
                                {
                                    var week = context.CultureInfo.Calendar.GetWeekOfYear(value.DateTime, CalendarWeekRule.FirstFullWeek, DayOfWeek.Monday);
                                    var year = week >= 52 && value.DateTime.Month == 1 ? value.Year - 1 : value.Year;
                                    result.Append(Format(year % 100));
                                    break;
                                }
                            case 'G':
                                {
                                    var week = context.CultureInfo.Calendar.GetWeekOfYear(value.DateTime, CalendarWeekRule.FirstFullWeek, DayOfWeek.Monday);
                                    var year = week >= 52 && value.DateTime.Month == 1 ? value.Year - 1 : value.Year;
                                    result.Append(Format(year));
                                    break;
                                }
                            case 'h':
                                ForStrf(value, "%b", result);
                                break;
                            case 'H':
                                result.Append(value.ToString("HH"));
                                break;
                            case 'I':
                                {
                                    var hour = value.Hour switch
                                    {
                                        0 => 12,
                                        <= 12 => value.Hour,
                                        _ => value.Hour - 12
                                    };

                                    result.Append(Format(hour, 2));
                                    break;
                                }
                            case 'j': result.Append(Format(value.DayOfYear, 3)); break;
                            case 'k': result.Append(value.Hour); break;
                            case 'l':
                                {
                                    useSpaceForPaddingFlag = true;
                                    var hour = value.Hour switch
                                    {
                                        0 => 12,
                                        <= 12 => value.Hour,
                                        _ => value.Hour - 12
                                    };

                                    result.Append(Format(hour, 2));
                                    break;
                                }
                            case 'L': result.Append(Format(value.Millisecond, 3)); break;
                            case 'm': result.Append(Format(value.Month, 2)); break;
                            case 'M':
                                result.Append(Format(value.Minute, 2));
                                break;
                            case 'n': result.Append(new String('\n', width ?? 1)); break;
                            case 'N':
                                width ??= 9;
                                var v = (value.Ticks % 10000000).ToString(context.CultureInfo);
                                result.Append(v.Length >= width ? v.Substring(0, width.Value) : v.PadRight(width.Value, '0'));
                                break;
                            case 'p': result.Append(value.ToString("tt", context.CultureInfo).ToUpperInvariant()); break;
                            case 'P': result.Append(value.ToString("tt", context.CultureInfo).ToLowerInvariant()); break;
                            case 'r':
                                {
                                    var sb = new StringBuilder();
                                    ForStrf(value, "%I:%M:%S %p", sb);
                                    result.Append(upperCaseFlag ? sb.ToString().ToUpperInvariant() : sb.ToString());
                                    break;
                                }
                            case 'R':
                                {
                                    var sb = new StringBuilder();
                                    ForStrf(value, "%H:%M", sb);
                                    result.Append(upperCaseFlag ? sb.ToString().ToUpperInvariant() : sb.ToString());
                                    break;
                                }
                            case 's': result.Append(Format(value.ToUnixTimeSeconds())); break;
                            case 'S':
                                result.Append(Format(value.Second, 2));
                                break;
                            case 't': result.Append(new String('\t', width ?? 1)); break;
                            case 'T':
                                {
                                    var sb = new StringBuilder();
                                    ForStrf(value, "%H:%M:%S", sb);
                                    result.Append(upperCaseFlag ? sb.ToString().ToUpperInvariant() : sb.ToString());
                                    break;
                                }
                            case 'u': result.Append(value.DayOfWeek switch { DayOfWeek.Sunday => 7, _ => (int)value.DayOfWeek }); break;
                            case 'U':
                                {
                                    var week = context.CultureInfo.Calendar.GetWeekOfYear(value.DateTime, CalendarWeekRule.FirstFullWeek, DayOfWeek.Sunday);
                                    if (week >= 52 && value.DateTime.Month == 1)
                                    {
                                        week = 0;
                                    }
                                    result.Append(Format(week, 2));
                                    break;
                                }
                            case 'v':
                                {
                                    var sb = new StringBuilder();
                                    ForStrf(value, "%e-%b-%Y", sb);
                                    result.Append(upperCaseFlag ? sb.ToString().ToUpperInvariant() : sb.ToString());
                                    break;
                                }
                            case 'V': result.Append(Format(value.DayOfYear / 7 + 1, 2)); break;
                            case 'w': result.Append(((int)value.DayOfWeek).ToString(context.CultureInfo)); break;
                            case 'W':
                                {
                                    var week = context.CultureInfo.Calendar.GetWeekOfYear(value.DateTime, CalendarWeekRule.FirstFullWeek, DayOfWeek.Monday);
                                    if (week >= 52 && value.DateTime.Month == 1)
                                    {
                                        week = 0;
                                    }
                                    result.Append(Format(week, 2));
                                    break;
                                }
                            case 'x':
                                {
                                    // x is defined as "%m/%d/%y" but it's also supposed to be locale aware, so we are using the 
                                    // C# short date pattern standard format instead

                                    result.Append(upperCaseFlag ? value.ToString("d", context.CultureInfo).ToUpperInvariant() : value.ToString("d", context.CultureInfo));
                                    break;
                                }
                            case 'X':
                                {
                                    // X is defined as "%T" but it's also supposed to be locale aware, so we are using the 
                                    // C# short time pattern standard format instead

                                    result.Append(upperCaseFlag ? value.ToString("t", context.CultureInfo).ToUpperInvariant() : value.ToString("t", context.CultureInfo));
                                    break;
                                }
                            case 'y':
                                result.Append(Format(value.Year % 100, 2));
                                break;
                            case 'Y':
                                result.Append(Format(value.Year, 4));
                                break;
                            case 'z':
                                {
                                    var zzz = value.ToString("zzz", context.CultureInfo);
                                    result.Append(useColonsForZeeDirectiveFlag ? zzz : zzz.Replace(":", ""));
                                    break;
                                }
                            case 'Z':
                                result.Append(value.ToString("zzz", context.CultureInfo));
                                break;
                            case '%': result.Append('%'); break;
                            case '+':
                                {
                                    var sb = new StringBuilder();
                                    ForStrf(value, "%a %b %e %H:%M:%S %Z %Y", sb);
                                    result.Append(upperCaseFlag ? sb.ToString().ToUpperInvariant() : sb.ToString());
                                    break;
                                }
                            default: result.Append('%').Append(c); break;
                        }

                        percent = false;
                        removeLeadingZerosFlag = false;
                        useSpaceForPaddingFlag = false;
                        upperCaseFlag = false;
                        useColonsForZeeDirectiveFlag = false;
                        width = null;

                        string Format(long value, int defaultWidth = 0)
                        {
                            var stringValue = value.ToString(context.CultureInfo);

                            if (removeLeadingZerosFlag)
                            {
                                return stringValue.TrimStart('0');
                            }

                            return stringValue.PadLeft(width == null ? defaultWidth : width.Value, useSpaceForPaddingFlag ? ' ' : '0');
                        }
                    }
                }
            }
        }

        public static ValueTask<FluidValue> FormatDate(FluidValue input, FilterArguments arguments, TemplateContext context)
        {
            if (!input.TryGetDateTimeInput(context, out var value))
            {
                return NilValue.Instance;
            }

            if (arguments.At(0).IsNil())
            {
                return NilValue.Instance;
            }

            var format = arguments.At(0).ToStringValue();

            var culture = context.CultureInfo;

            if (!arguments.At(1).IsNil())
            {
                culture = CultureInfo.CreateSpecificCulture(arguments.At(1).ToStringValue()) ?? context.CultureInfo;
            }

            return new StringValue(value.ToString(format, culture));
        }

        private static async ValueTask WriteJson(Utf8JsonWriter writer, FluidValue input, TemplateContext ctx, HashSet<object> stack = null)
        {
            switch (input.Type)
            {
                case FluidValues.Array:
                    writer.WriteStartArray();
                    foreach (var item in input.Enumerate(ctx))
                    {
                        await WriteJson(writer, item, ctx);
                    }
                    writer.WriteEndArray();
                    break;
                case FluidValues.Boolean:
                    writer.WriteBooleanValue(input.ToBooleanValue());
                    break;
                case FluidValues.Nil:
                    writer.WriteNullValue();
                    break;
                case FluidValues.Number:
                    writer.WriteNumberValue(input.ToNumberValue());
                    break;
                case FluidValues.Dictionary:
                    if (input.ToObjectValue() is IFluidIndexable dic)
                    {
                        writer.WriteStartObject();
                        foreach (var key in dic.Keys)
                        {
                            writer.WritePropertyName(key);
                            if (dic.TryGetValue(key, out var value))
                            {
                                await WriteJson(writer, value, ctx);
                            }
                            else
                            {
                                await WriteJson(writer, NilValue.Instance, ctx);
                            }
                        }

                        writer.WriteEndObject();
                    }
                    else
                    {
                        writer.WriteNullValue();
                    }
                    break;
                case FluidValues.Object:
                    var obj = input.ToObjectValue();
                    if (obj != null)
                    {
                        writer.WriteStartObject();
                        var type = obj.GetType();
                        var properties = type.GetProperties(BindingFlags.Instance | BindingFlags.Public);
                        var strategy = ctx.Options.MemberAccessStrategy;

                        var conv = strategy.MemberNameStrategy;
                        foreach (var property in properties)
                        {
                            var name = conv(property);
#pragma warning disable CA1859 // Change type of variable 'fluidValue' from 'Fluid.Values.FluidValue' to 'Fluid.Values.StringValue' for improved performance
                            var fluidValue = await input.GetValueAsync(name, ctx);
#pragma warning restore CA1859
                            if (fluidValue.IsNil())
                            {
                                continue;
                            }

                            stack ??= new HashSet<object>();
                            if (fluidValue is ObjectValue)
                            {
                                var value = fluidValue.ToObjectValue();
                                if (stack.Contains(value))
                                {
                                    fluidValue = StringValue.Create("Circular reference has been detected.");
                                }
                            }

                            writer.WritePropertyName(name);
                            stack.Add(obj);
                            await WriteJson(writer, fluidValue, ctx, stack);
                            stack.Remove(obj);
                        }

                        writer.WriteEndObject();
                    }
                    else
                    {
                        writer.WriteNullValue();
                    }
                    break;
                case FluidValues.DateTime:
                    var objValue = input.ToObjectValue();
                    if (objValue is DateTime dateTime)
                    {
                        writer.WriteStringValue(dateTime);
                    }
                    else if (objValue is DateTimeOffset dateTimeOffset)
                    {
                        writer.WriteStringValue(dateTimeOffset);
                    }
                    else
                    {
                        writer.WriteStringValue(Convert.ToDateTime(objValue));
                    }
                    break;
                case FluidValues.String:
                    writer.WriteStringValue(input.ToStringValue());
                    break;
                case FluidValues.Blank:
                    writer.WriteStringValue(string.Empty);
                    break;
                case FluidValues.Empty:
                    writer.WriteStringValue(string.Empty);
                    break;
                default:
                    throw new NotSupportedException("Unrecognized FluidValue");
            }
        }

        public static async ValueTask<FluidValue> Json(FluidValue input, FilterArguments arguments, TemplateContext context)
        {
            using var ms = new MemoryStream();
            await using (var writer = new Utf8JsonWriter(ms, new JsonWriterOptions
            {
                Indented = arguments.At(0).ToBooleanValue()
            }))
            {
                await WriteJson(writer, input, context);
            }

            ms.Seek(0, SeekOrigin.Begin);
            using var sr = new StreamReader(ms, Encoding.UTF8);
            var json = await sr.ReadToEndAsync();
            return new StringValue(json);
        }

        public static ValueTask<FluidValue> FormatNumber(FluidValue input, FilterArguments arguments, TemplateContext context)
        {
            if (arguments.At(0).IsNil())
            {
                return NilValue.Instance;
            }

            var format = arguments.At(0).ToStringValue();

            var culture = context.CultureInfo;

            if (!arguments.At(1).IsNil())
            {
                culture = CultureInfo.CreateSpecificCulture(arguments.At(1).ToStringValue()) ?? context.CultureInfo;
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

            var culture = context.CultureInfo;

            if (arguments.HasNamed("culture"))
            {
                culture = CultureInfo.CreateSpecificCulture(arguments["culture"].ToStringValue()) ?? context.CultureInfo;
            }

            var parameters = arguments.ValuesToObjectArray();

            return new StringValue(string.Format(culture, format, parameters));
        }

        public static ValueTask<FluidValue> MD5(FluidValue input, FilterArguments arguments, TemplateContext context)
        {
            var value = input.ToStringValue();
            if (string.IsNullOrEmpty(value))
            {
                return StringValue.Empty;
            }

#pragma warning disable CA5351 // Do Not Use Broken Cryptographic Algorithms
            using var provider = System.Security.Cryptography.MD5.Create();
#pragma warning restore CA5351
            var builder = new StringBuilder(32);
#pragma warning disable CA1850 // Prefer static 'System.Security.Cryptography.MD5.HashData' method over 'ComputeHash'
            foreach (byte b in provider.ComputeHash(Encoding.UTF8.GetBytes(value)))
#pragma warning restore CA1850
            {
                builder.Append(b.ToString("x2").ToLowerInvariant());
            }

            return new StringValue(builder.ToString());
        }

        public static ValueTask<FluidValue> Sha1(FluidValue input, FilterArguments arguments, TemplateContext context)
        {
            var value = input.ToStringValue();
            if (string.IsNullOrEmpty(value))
            {
                return StringValue.Empty;
            }

#pragma warning disable CA5350 // Do Not Use Weak Cryptographic Algorithms
            using var provider = System.Security.Cryptography.SHA1.Create();
#pragma warning restore CA5350
            var builder = new StringBuilder(40);
#pragma warning disable CA1850 // Prefer static 'System.Security.Cryptography.MD5.HashData' method over 'ComputeHash'
            foreach (byte b in provider.ComputeHash(Encoding.UTF8.GetBytes(value)))
#pragma warning restore CA1850
            {
                builder.Append(b.ToString("x2").ToLowerInvariant());
            }

            return new StringValue(builder.ToString());
        }

        public static ValueTask<FluidValue> Sha256(FluidValue input, FilterArguments arguments, TemplateContext context)
        {
            var value = input.ToStringValue();
            if (string.IsNullOrEmpty(value))
            {
                return StringValue.Empty;
            }

            using var provider = System.Security.Cryptography.SHA256.Create();
            var builder = new StringBuilder(64);
#pragma warning disable CA1850 // Prefer static 'System.Security.Cryptography.MD5.HashData' method over 'ComputeHash'
            foreach (byte b in provider.ComputeHash(Encoding.UTF8.GetBytes(value)))
#pragma warning restore CA1850
            {
                builder.Append(b.ToString("x2").ToLowerInvariant());
            }

            return new StringValue(builder.ToString());
        }
    }
}
