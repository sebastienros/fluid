using System.Globalization;
using System.Text;

namespace Fluid.Filters;

/// <summary>
/// Formats a date like Ruby's <c>Time#strftime</c>, which Liquid's date filter uses:
/// https://docs.ruby-lang.org/en/master/strftime_formatting_rdoc.html
/// Day and month names and the AM/PM designators come from the culture of the context.
/// </summary>
internal static class Strftime
{
    private const int Left = 1;
    private const int Upper = 2;
    private const int Lower = 4;
    private const int ChangeCase = 8;

    public static void Format(DateTimeOffset value, string format, TemplateContext context, ref ValueStringBuilder result)
    {
        var culture = context.CultureInfo;

        for (var i = 0; i < format.Length; i++)
        {
            var c = format[i];

            if (c != '%')
            {
                result.Append(c);
                continue;
            }

            var start = i;
            var flags = 0;
            var padding = '\0';
            var precision = -1;
            var colons = 0;

            while (true)
            {
                // Like Ruby, a format that ends inside a conversion is invalid.
                if (++i == format.Length)
                {
                    throw new LiquidException($"invalid format: {format}");
                }

                c = format[i];

                if (c is >= '0' and <= '9')
                {
                    // A leading zero is the zero padding flag, and also part of the width.
                    if (c == '0')
                    {
                        padding = '0';
                    }

                    precision = 0;
                    while (i < format.Length && format[i] is >= '0' and <= '9')
                    {
                        precision = Math.Min(precision * 10 + (format[i] - '0'), 1024);
                        i++;
                    }

                    i--;
                    continue;
                }

                // Like Ruby, flags must come before the width: after it, the conversion is written as is.
                if (precision > 0 && c is '-' or '_' or '^' or '#')
                {
                    result.Append(format.AsSpan(start, i - start + 1));
                    break;
                }

                switch (c)
                {
                    case '-':
                        flags |= Left;
                        padding = '\0';
                        precision = 0;
                        continue;

                    case '_':
                        padding = ' ';
                        continue;

                    case '^':
                        flags |= Upper;
                        continue;

                    case '#':
                        flags |= ChangeCase;
                        continue;

                    case ':':
                        {
                            // Colons only apply to z: %:z, %::z and %:::z.
                            var count = 0;
                            while (i + count < format.Length && format[i + count] == ':')
                            {
                                count++;
                            }

                            if (count > 3 || i + count == format.Length || format[i + count] != 'z')
                            {
                                goto default;
                            }

                            colons = count;
                            i += count - 1;
                            continue;
                        }

                    // The E and O modifiers select alternative representations, which Ruby ignores.
                    case 'E':
                        if (i + 1 < format.Length && "cCxXyY".Contains(format[i + 1]))
                        {
                            continue;
                        }

                        goto default;

                    case 'O':
                        if (i + 1 < format.Length && "deHkIlmMSuUVwWy".Contains(format[i + 1]))
                        {
                            continue;
                        }

                        goto default;

                    case 'a':
                        AppendName(ref result, culture.DateTimeFormat.GetAbbreviatedDayName(value.DayOfWeek), flags, padding, precision, culture);
                        break;
                    case 'A':
                        AppendName(ref result, culture.DateTimeFormat.GetDayName(value.DayOfWeek), flags, padding, precision, culture);
                        break;
                    case 'b':
                    case 'h':
                        AppendName(ref result, culture.DateTimeFormat.GetAbbreviatedMonthName(value.Month), flags, padding, precision, culture);
                        break;
                    case 'B':
                        AppendName(ref result, culture.DateTimeFormat.GetMonthName(value.Month), flags, padding, precision, culture);
                        break;

                    case 'p':
                    case 'P':
                        {
                            // %p is upper case unless the # flag changes it, %P lower case unless ^ or # is used.
                            if ((c == 'p' && (flags & ChangeCase) != 0) || (c == 'P' && (flags & (ChangeCase | Upper)) == 0))
                            {
                                flags = (flags & ~(Upper | ChangeCase)) | Lower;
                            }

                            var designator = value.Hour < 12 ? culture.DateTimeFormat.AMDesignator : culture.DateTimeFormat.PMDesignator;
                            AppendText(ref result, designator.ToUpper(culture), flags, padding, precision, culture);
                            break;
                        }

                    case 'Z':
                        if ((flags & ChangeCase) != 0)
                        {
                            flags = (flags & ~(Upper | ChangeCase)) | Lower;
                        }

                        // .NET has no time zone abbreviations: UTC is named, other zones are written as their offset.
                        AppendText(ref result, value.Offset == TimeSpan.Zero ? "UTC" : value.ToString("zzz", CultureInfo.InvariantCulture), flags, padding, precision, culture);
                        break;

                    case '%':
                        AppendText(ref result, "%", flags, padding, precision, culture);
                        break;
                    case 'n':
                        AppendText(ref result, "\n", flags, padding, precision, culture);
                        break;
                    case 't':
                        AppendText(ref result, "\t", flags, padding, precision, culture);
                        break;

                    case 'C': AppendNumber(ref result, FloorDivide(value.Year, 100), '0', 2, flags, padding, precision); break;
                    case 'd': AppendNumber(ref result, value.Day, '0', 2, flags, padding, precision); break;
                    case 'e': AppendNumber(ref result, value.Day, ' ', 2, flags, padding, precision); break;
                    case 'H': AppendNumber(ref result, value.Hour, '0', 2, flags, padding, precision); break;
                    case 'k': AppendNumber(ref result, value.Hour, ' ', 2, flags, padding, precision); break;
                    case 'I': AppendNumber(ref result, Hour12(value), '0', 2, flags, padding, precision); break;
                    case 'l': AppendNumber(ref result, Hour12(value), ' ', 2, flags, padding, precision); break;
                    case 'j': AppendNumber(ref result, value.DayOfYear, '0', 3, flags, padding, precision); break;
                    case 'm': AppendNumber(ref result, value.Month, '0', 2, flags, padding, precision); break;
                    case 'M': AppendNumber(ref result, value.Minute, '0', 2, flags, padding, precision); break;
                    case 'S': AppendNumber(ref result, value.Second, '0', 2, flags, padding, precision); break;
                    case 's': AppendNumber(ref result, value.ToUnixTimeSeconds(), '0', 1, flags, padding, precision); break;
                    case 'u': AppendNumber(ref result, value.DayOfWeek == DayOfWeek.Sunday ? 7 : (int)value.DayOfWeek, '0', 1, flags, padding, precision); break;
                    case 'w': AppendNumber(ref result, (int)value.DayOfWeek, '0', 1, flags, padding, precision); break;
                    case 'U': AppendNumber(ref result, (value.DayOfYear - 1 + 7 - (int)value.DayOfWeek) / 7, '0', 2, flags, padding, precision); break;
                    case 'W': AppendNumber(ref result, (value.DayOfYear - 1 + 7 - (value.DayOfWeek == DayOfWeek.Sunday ? 6 : (int)value.DayOfWeek - 1)) / 7, '0', 2, flags, padding, precision); break;
                    case 'V': AppendNumber(ref result, GetIsoWeekOfYear(value.DateTime), '0', 2, flags, padding, precision); break;
                    case 'y': AppendNumber(ref result, value.Year % 100, '0', 2, flags, padding, precision); break;
                    case 'g': AppendNumber(ref result, GetIsoWeekYear(value.DateTime) % 100, '0', 2, flags, padding, precision); break;
                    case 'Y': AppendNumber(ref result, value.Year, '0', 4, flags, padding, precision); break;
                    case 'G': AppendNumber(ref result, GetIsoWeekYear(value.DateTime), '0', 4, flags, padding, precision); break;

                    // Fractional seconds: the width is the number of digits, which are truncated, not rounded.
                    case 'L':
                    case 'N':
                        {
                            var digits = precision > 0 ? precision : c == 'L' ? 3 : 9;
                            var fraction = (value.Ticks % TimeSpan.TicksPerSecond).ToString("D7", CultureInfo.InvariantCulture);

                            if (digits <= fraction.Length)
                            {
                                result.Append(fraction.AsSpan(0, digits));
                            }
                            else
                            {
                                result.Append(fraction);
                                result.Append('0', digits - fraction.Length);
                            }

                            break;
                        }

                    case 'z':
                        AppendOffset(ref result, value.Offset, colons, padding, precision);
                        break;

                    // Combinations, which are padded and upper cased as a whole.
                    case 'c': AppendCombination(ref result, value, "%a %b %e %H:%M:%S %Y", flags, padding, precision, context); break;
                    case 'x':
                    case 'D': AppendCombination(ref result, value, "%m/%d/%y", flags, padding, precision, context); break;
                    case 'X':
                    case 'T': AppendCombination(ref result, value, "%H:%M:%S", flags, padding, precision, context); break;
                    case 'F': AppendCombination(ref result, value, "%Y-%m-%d", flags, padding, precision, context); break;
                    case 'R': AppendCombination(ref result, value, "%H:%M", flags, padding, precision, context); break;
                    case 'r': AppendCombination(ref result, value, "%I:%M:%S %p", flags, padding, precision, context); break;
                    case 'v': AppendCombination(ref result, value, "%e-%^b-%4Y", flags, padding, precision, context); break;

                    // Not supported by Ruby, kept from date(1).
                    case '+': AppendCombination(ref result, value, "%a %b %e %H:%M:%S %Z %Y", flags, padding, precision, context); break;

                    // Like Ruby, an unknown conversion is written as is.
                    default:
                        result.Append(format.AsSpan(start, i - start + 1));
                        break;
                }

                break;
            }
        }
    }

    private static int Hour12(DateTimeOffset value) => value.Hour % 12 == 0 ? 12 : value.Hour % 12;

    private static int FloorDivide(int value, int divisor) => value >= 0 ? value / divisor : -((-value + divisor - 1) / divisor);

    // Like printf's "%0*d" and "%*d": the sign comes before zeros, after spaces.
    private static void AppendNumber(ref ValueStringBuilder result, long number, char defaultPadding, int defaultPrecision, int flags, char padding, int precision)
    {
        var width = (flags & Left) != 0 ? 1 : precision <= 0 ? defaultPrecision : precision;
        var zeros = padding == '0' || (padding == '\0' && defaultPadding == '0');

        Span<char> buffer = stackalloc char[20];
        var negative = number < 0;
        var magnitude = negative ? (ulong)(-(number + 1)) + 1 : (ulong)number;
        var position = buffer.Length;

        do
        {
            buffer[--position] = (char)('0' + (int)(magnitude % 10));
            magnitude /= 10;
        }
        while (magnitude != 0);

        var digits = buffer.Slice(position);
        var length = digits.Length + (negative ? 1 : 0);

        if (zeros)
        {
            if (negative)
            {
                result.Append('-');
            }

            if (width > length)
            {
                result.Append('0', width - length);
            }
        }
        else
        {
            if (width > length)
            {
                result.Append(' ', width - length);
            }

            if (negative)
            {
                result.Append('-');
            }
        }

        result.Append(digits);
    }

    private static void AppendName(ref ValueStringBuilder result, string name, int flags, char padding, int precision, CultureInfo culture)
    {
        // The # flag upper cases names.
        if ((flags & ChangeCase) != 0)
        {
            flags = (flags & ~(Lower | ChangeCase)) | Upper;
        }

        AppendText(ref result, name, flags, padding, precision, culture);
    }

    private static void AppendText(ref ValueStringBuilder result, string text, int flags, char padding, int precision, CultureInfo culture)
    {
        if ((flags & Left) == 0 && precision > text.Length)
        {
            result.Append(padding == '\0' ? ' ' : padding, precision - text.Length);
        }

        if ((flags & Upper) != 0)
        {
            text = text.ToUpper(culture);
        }
        else if ((flags & Lower) != 0)
        {
            text = text.ToLower(culture);
        }

        result.Append(text);
    }

    private static void AppendCombination(ref ValueStringBuilder result, DateTimeOffset value, string format, int flags, char padding, int precision, TemplateContext context)
    {
        var inner = new ValueStringBuilder(stackalloc char[64]);
        Format(value, format, context, ref inner);

        var text = inner.ToString();
        if ((flags & Upper) != 0)
        {
            text = text.ToUpper(context.CultureInfo);
        }

        // Like Ruby, the width applies even with the - flag.
        if (precision > text.Length)
        {
            result.Append(padding == '\0' ? ' ' : padding, precision - text.Length);
        }

        result.Append(text);
    }

    // %z is +hhmm, %:z +hh:mm, %::z +hh:mm:ss and %:::z only as precise as needed. A width applies to the hours,
    // padded with zeros after the sign, or with spaces before it with the _ flag.
    private static void AppendOffset(ref ValueStringBuilder result, TimeSpan offset, int colons, char padding, int precision)
    {
        var seconds = (long)offset.TotalSeconds;
        var negative = seconds < 0;
        var remaining = Math.Abs(seconds);

        var hourDigits = colons switch
        {
            0 => precision <= 5 ? 2 : precision - 3,
            1 => precision <= 6 ? 2 : precision - 4,
            2 => precision <= 9 ? 2 : precision - 7,
            _ => remaining % 3600 == 0 ? (precision <= 3 ? 2 : precision - 1)
                : remaining % 60 == 0 ? (precision <= 6 ? 2 : precision - 4)
                : (precision <= 9 ? 2 : precision - 7),
        };

        var hours = (remaining / 3600).ToString(CultureInfo.InvariantCulture);
        var sign = negative ? '-' : '+';

        if (padding == ' ')
        {
            if (hourDigits > hours.Length)
            {
                result.Append(' ', hourDigits - hours.Length);
            }

            result.Append(sign);
            result.Append(hours);
        }
        else
        {
            result.Append(sign);
            if (hourDigits > hours.Length)
            {
                result.Append('0', hourDigits - hours.Length);
            }

            result.Append(hours);
        }

        remaining %= 3600;
        if (colons == 3 && remaining == 0)
        {
            return;
        }

        if (colons >= 1)
        {
            result.Append(':');
        }

        AppendTwoDigits(ref result, remaining / 60);

        remaining %= 60;
        if (colons == 3 && remaining == 0)
        {
            return;
        }

        if (colons >= 2)
        {
            result.Append(':');
            AppendTwoDigits(ref result, remaining);
        }
    }

    private static void AppendTwoDigits(ref ValueStringBuilder result, long value)
    {
        result.Append((char)('0' + value / 10));
        result.Append((char)('0' + value % 10));
    }

    // ISO 8601 weeks start on Monday, and week 1 is the one holding the year's first Thursday, so
    // the first and last days of a year can belong to a week of the next or previous year.
    // ISOWeek does this on .NET Core 3.0 and later, but not on netstandard2.0.
    private static int GetIsoWeekOfYear(DateTime date)
    {
        var week = GetUncorrectedIsoWeek(date);

        if (week < 1)
        {
            return GetIsoWeeksInYear(date.Year - 1);
        }

        if (week > GetIsoWeeksInYear(date.Year))
        {
            return 1;
        }

        return week;
    }

    private static int GetIsoWeekYear(DateTime date)
    {
        var week = GetUncorrectedIsoWeek(date);

        if (week < 1)
        {
            return date.Year - 1;
        }

        if (week > GetIsoWeeksInYear(date.Year))
        {
            return date.Year + 1;
        }

        return date.Year;
    }

    private static int GetUncorrectedIsoWeek(DateTime date)
    {
        var isoDayOfWeek = date.DayOfWeek == DayOfWeek.Sunday ? 7 : (int)date.DayOfWeek;
        return (date.DayOfYear - isoDayOfWeek + 10) / 7;
    }

    private static int GetIsoWeeksInYear(int year)
    {
        static int DayOfWeekOfLastDay(int y) => (y + y / 4 - y / 100 + y / 400) % 7;

        if (DayOfWeekOfLastDay(year) == 4 || DayOfWeekOfLastDay(year - 1) == 3)
        {
            return 53;
        }

        return 52;
    }
}
