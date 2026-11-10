using System.Text.RegularExpressions;

namespace Fluid.Values;

/// <summary>
/// Converts strings to dates like Ruby's <c>Time.parse</c>, which Liquid's <c>date</c> filter uses.
/// It ports the heuristics of <c>Date._parse</c> (ext/date/date_parse.c) and the way <c>Time.parse</c>
/// completes and validates its result (time.rb).
/// </summary>
/// <remarks>
/// Each step searches the first match of a pattern in the lowercased string, reads the fields it finds and
/// replaces the match with a space, so that later steps don't read the same text again. The context's time zone
/// stands for Ruby's local time zone.
/// </remarks>
internal static partial class RubyTimeParser
{
    // Date._parse rejects longer strings.
    private const int MaxLength = 128;

    private const RegexOptions Options = RegexOptions.CultureInvariant;

    private const string Months = "jan|feb|mar|apr|may|jun|jul|aug|sep|oct|nov|dec";
    private const string Era = @"(c(?:e|\.e\.)|b(?:ce|\.c\.e\.)|a(?:d|\.d\.)|b(?:c|\.c\.))?";

    // Everything but these characters is a separator.
    private const string SeparatorsPattern = @"[^-+',./:@\p{L}\p{M}\p{Nd}\[\]]+";
    private const string DayPattern = @"\b(?:sun|mon|tue|wed|thu|fri|sat)[^-/0-9\s]*";
    private const string TimePattern =
        @"((?:[0-9]+\s*(?::\s*[0-9]+(?:\s*:\s*[0-9]+(?:[,.][0-9]*)?)?|h(?:\s*[0-9]+m?(?:\s*[0-9]+s?)?)?)(?:\s*[ap](?:m\b|\.m\.))?|[0-9]+\s*[ap](?:m\b|\.m\.)))" +
        @"(?:\s*((?:gmt|utc?)?[-+][0-9]+(?:[,.:][0-9]+(?::[0-9]+)?)?|[\p{L}.\s]+(?:standard|daylight)\stime\b|\p{L}+(?:\sdst)?\b))?";
    private const string TimeFieldsPattern = @"\A([0-9]+)h?(?:\s*:?\s*([0-9]+)m?(?:\s*:?\s*([0-9]+)(?:[,.]([0-9]+))?s?)?)?(?:\s*([ap])(?:m\b|\.m\.))?";
    private const string EuPattern = @"('?[0-9]+)[^-0-9\s]*\s*(" + Months + @")[^-0-9\s']*(?:\s*" + Era + @"\s*('?-?[0-9]+(?:(?:st|nd|rd|th)\b)?))?";
    private const string UsPattern = @"\b(" + Months + @")[^-0-9\s']*\s*('?[0-9]+)[^-0-9\s']*(?:\s*,?\s*" + Era + @"\s*('?-?[0-9]+))?";
    private const string IsoPattern = @"('?[-+]?[0-9]+)-([0-9]+)-('?-?[0-9]+)";
    private const string JisPattern = @"\b([mtshr])([0-9]+)\.([0-9]+)\.([0-9]+)";
    private const string Vms1Pattern = @"('?-?[0-9]+)-(" + Months + @")[^-/.]*-('?-?[0-9]+)";
    private const string Vms2Pattern = @"\b(" + Months + @")[^-/.]*-('?-?[0-9]+)(?:-('?-?[0-9]+))?";
    private const string SlashPattern = @"('?-?[0-9]+)/\s*('?[0-9]+)(?:[^0-9]\s*('?-?[0-9]+))?";
    private const string DotPattern = @"('?-?[0-9]+)\.\s*('?[0-9]+)\.\s*('?-?[0-9]+)";
    private const string IsoWeekPattern = @"\b(?:[0-9]{2}|[0-9]{4})?-?w[0-9]{2}(?:-?[0-9])?\b";
    private const string IsoWeekDayPattern = @"-w-[0-9]\b";
    private const string IsoMonthDayPattern = @"--([0-9]{2})?-([0-9]{2})\b";
    private const string IsoMonthDay2Pattern = @"--([0-9]{2})([0-9]{2})?\b";
    private const string IsoOrdinalGuardPattern = @"[,.](?:[0-9]{2}|[0-9]{4})-[0-9]{3}\b";
    private const string IsoOrdinalPattern = @"\b([0-9]{2}|[0-9]{4})-([0-9]{3})\b";
    private const string IsoYearDayGuardPattern = @"[0-9]-[0-9]{3}\b";
    private const string IsoYearDayPattern = @"\b-([0-9]{3})\b";
    private const string YearPattern = @"'([0-9]+)\b";
    private const string MonthPattern = @"\b(" + Months + @")\S*";
    private const string MonthDayPattern = @"([0-9]+)(?:st|nd|rd|th)\b";
    private const string DigitsPattern = @"([-+]?)([0-9]{2,14})(?:\s*t?\s*([0-9]{2,6})?(?:[,.]([0-9]*))?)?(?:\s*(z\b|[-+][0-9]{1,4}\b|\[[-+]?[0-9][^\]]*\]))?";
    private const string BcPattern = @"\b(?:bc\b|bce\b|b\.c\.|b\.c\.e\.)";
    private const string FragmentPattern = @"\A\s*([0-9]{1,2})\s*\z";

#if NET8_0_OR_GREATER
    [GeneratedRegex(SeparatorsPattern, Options)] private static partial Regex SeparatorsRegex();
    [GeneratedRegex(DayPattern, Options)] private static partial Regex DayRegex();
    [GeneratedRegex(TimePattern, Options)] private static partial Regex TimeRegex();
    [GeneratedRegex(TimeFieldsPattern, Options)] private static partial Regex TimeFieldsRegex();
    [GeneratedRegex(EuPattern, Options)] private static partial Regex EuRegex();
    [GeneratedRegex(UsPattern, Options)] private static partial Regex UsRegex();
    [GeneratedRegex(IsoPattern, Options)] private static partial Regex IsoRegex();
    [GeneratedRegex(JisPattern, Options)] private static partial Regex JisRegex();
    [GeneratedRegex(Vms1Pattern, Options)] private static partial Regex Vms1Regex();
    [GeneratedRegex(Vms2Pattern, Options)] private static partial Regex Vms2Regex();
    [GeneratedRegex(SlashPattern, Options)] private static partial Regex SlashRegex();
    [GeneratedRegex(DotPattern, Options)] private static partial Regex DotRegex();
    [GeneratedRegex(IsoWeekPattern, Options)] private static partial Regex IsoWeekRegex();
    [GeneratedRegex(IsoWeekDayPattern, Options)] private static partial Regex IsoWeekDayRegex();
    [GeneratedRegex(IsoMonthDayPattern, Options)] private static partial Regex IsoMonthDayRegex();
    [GeneratedRegex(IsoMonthDay2Pattern, Options)] private static partial Regex IsoMonthDay2Regex();
    [GeneratedRegex(IsoOrdinalGuardPattern, Options)] private static partial Regex IsoOrdinalGuardRegex();
    [GeneratedRegex(IsoOrdinalPattern, Options)] private static partial Regex IsoOrdinalRegex();
    [GeneratedRegex(IsoYearDayGuardPattern, Options)] private static partial Regex IsoYearDayGuardRegex();
    [GeneratedRegex(IsoYearDayPattern, Options)] private static partial Regex IsoYearDayRegex();
    [GeneratedRegex(YearPattern, Options)] private static partial Regex YearRegex();
    [GeneratedRegex(MonthPattern, Options)] private static partial Regex MonthRegex();
    [GeneratedRegex(MonthDayPattern, Options)] private static partial Regex MonthDayRegex();
    [GeneratedRegex(DigitsPattern, Options)] private static partial Regex DigitsRegex();
    [GeneratedRegex(BcPattern, Options)] private static partial Regex BcRegex();
    [GeneratedRegex(FragmentPattern, Options)] private static partial Regex FragmentRegex();
#else
    private static Regex _separators, _day, _time, _timeFields, _eu, _us, _iso, _jis, _vms1, _vms2, _slash, _dot, _isoWeek, _isoWeekDay,
        _isoMonthDay, _isoMonthDay2, _isoOrdinalGuard, _isoOrdinal, _isoYearDayGuard, _isoYearDay, _year, _month, _monthDay, _digits, _bc, _fragment;

    private static Regex Get(ref Regex regex, string pattern) => regex ??= new Regex(pattern, Options);

    private static Regex SeparatorsRegex() => Get(ref _separators, SeparatorsPattern);
    private static Regex DayRegex() => Get(ref _day, DayPattern);
    private static Regex TimeRegex() => Get(ref _time, TimePattern);
    private static Regex TimeFieldsRegex() => Get(ref _timeFields, TimeFieldsPattern);
    private static Regex EuRegex() => Get(ref _eu, EuPattern);
    private static Regex UsRegex() => Get(ref _us, UsPattern);
    private static Regex IsoRegex() => Get(ref _iso, IsoPattern);
    private static Regex JisRegex() => Get(ref _jis, JisPattern);
    private static Regex Vms1Regex() => Get(ref _vms1, Vms1Pattern);
    private static Regex Vms2Regex() => Get(ref _vms2, Vms2Pattern);
    private static Regex SlashRegex() => Get(ref _slash, SlashPattern);
    private static Regex DotRegex() => Get(ref _dot, DotPattern);
    private static Regex IsoWeekRegex() => Get(ref _isoWeek, IsoWeekPattern);
    private static Regex IsoWeekDayRegex() => Get(ref _isoWeekDay, IsoWeekDayPattern);
    private static Regex IsoMonthDayRegex() => Get(ref _isoMonthDay, IsoMonthDayPattern);
    private static Regex IsoMonthDay2Regex() => Get(ref _isoMonthDay2, IsoMonthDay2Pattern);
    private static Regex IsoOrdinalGuardRegex() => Get(ref _isoOrdinalGuard, IsoOrdinalGuardPattern);
    private static Regex IsoOrdinalRegex() => Get(ref _isoOrdinal, IsoOrdinalPattern);
    private static Regex IsoYearDayGuardRegex() => Get(ref _isoYearDayGuard, IsoYearDayGuardPattern);
    private static Regex IsoYearDayRegex() => Get(ref _isoYearDay, IsoYearDayPattern);
    private static Regex YearRegex() => Get(ref _year, YearPattern);
    private static Regex MonthRegex() => Get(ref _month, MonthPattern);
    private static Regex MonthDayRegex() => Get(ref _monthDay, MonthDayPattern);
    private static Regex DigitsRegex() => Get(ref _digits, DigitsPattern);
    private static Regex BcRegex() => Get(ref _bc, BcPattern);
    private static Regex FragmentRegex() => Get(ref _fragment, FragmentPattern);
#endif

    // Larger numbers are out of range for any field, and can't overflow when combined.
    private const long MaxNumber = 1_000_000_000_000;

    private struct Fields
    {
        public long? Year, Mon, MDay, YDay, Hour, Min, Sec;
        public long? FractionTicks;
        public string Zone;
        public bool Bc;
        public bool CompleteYear;
        public bool Invalid;
    }

    public static bool TryParse(string value, TemplateContext context, out DateTimeOffset result)
    {
        result = default;

        if (value.Length > MaxLength)
        {
            return false;
        }

        if (TryParseIsoDateTime(value, context, out result))
        {
            return true;
        }

        // Liquid downcases the string, Time.parse upcases the zone.
        var str = SeparatorsRegex().Replace(value.ToLowerInvariant(), " ");
        var fields = new Fields { CompleteYear = true };

        Substitute(ref str, DayRegex(), out _);
        ParseTime(ref str, ref fields);

        if (fields.Invalid)
        {
            return false;
        }

        _ = ParseEu(ref str, ref fields)
            || ParseUs(ref str, ref fields)
            || ParseIso(ref str, ref fields)
            || ParseJis(ref str, ref fields)
            || ParseVms(ref str, ref fields)
            || ParseSlashOrDot(ref str, ref fields, SlashRegex())
            || ParseSlashOrDot(ref str, ref fields, DotRegex())
            || ParseIso2(ref str, ref fields)
            || ParseSingle(ref str, YearRegex(), ref fields.Year)
            || ParseMonth(ref str, ref fields)
            || ParseSingle(ref str, MonthDayRegex(), ref fields.MDay)
            || ParseDigits(ref str, ref fields);

        if (Substitute(ref str, BcRegex(), out _))
        {
            fields.Bc = true;
        }

        ParseFragment(str, ref fields);

        if (fields.Bc && fields.Year is long bcYear)
        {
            fields.Year = 1 - bcYear;
        }

        if (fields.CompleteYear && fields.Year is long shortYear and >= 0 and <= 99)
        {
            fields.Year = shortYear + (shortYear >= 69 ? 1900 : 2000);
        }

        return MakeTime(ref fields, context, out result);
    }

    // yyyy-mm-dd, optionally followed by T or a space, hh:mm[:ss[.fraction]] and a zone: Z, UTC, GMT or ±hh[[:]mm].
    // Time.parse reads these common forms like ISO 8601: the ones that need its other rules, like an hour of 24,
    // a day after the end of the month or a large offset, are left to them.
    private static bool TryParseIsoDateTime(string s, TemplateContext context, out DateTimeOffset result)
    {
        result = default;
        var length = s.Length;

        if (length < 10 || s[4] != '-' || s[7] != '-'
            || !TryDigits(s, 0, 4, out var year) || !TryDigits(s, 5, 2, out var month) || !TryDigits(s, 8, 2, out var day)
            || year < 1 || month is < 1 or > 12 || day < 1 || day > DateTime.DaysInMonth(year, month))
        {
            return false;
        }

        int hour = 0, minute = 0, second = 0;
        long ticks = 0;
        int? offset = null;
        var i = 10;

        if (i < length)
        {
            if (s[i] is not ('T' or 't' or ' ') || !TryDigits(s, i + 1, 2, out hour) || i + 3 >= length || s[i + 3] != ':' || !TryDigits(s, i + 4, 2, out minute))
            {
                return false;
            }

            i += 6;

            if (i < length && s[i] == ':')
            {
                if (!TryDigits(s, i + 1, 2, out second))
                {
                    return false;
                }

                i += 3;

                if (i < length && s[i] is '.' or ',')
                {
                    var start = ++i;

                    while (i < length && IsDigit(s[i]))
                    {
                        i++;
                    }

                    if (i == start)
                    {
                        return false;
                    }

                    ticks = ToTicks(s, start, i - start);
                }
            }

            if (hour > 23 || minute > 59 || second > 59)
            {
                return false;
            }

            if (i < length && s[i] == ' ')
            {
                i++;
            }

            var rest = length - i;

            if (rest == 0)
            {
            }
            else if (rest == 1 && s[i] is 'Z' or 'z')
            {
                offset = 0;
            }
            else if (rest == 3 && (string.Compare(s, i, "UTC", 0, 3, StringComparison.OrdinalIgnoreCase) == 0 || string.Compare(s, i, "GMT", 0, 3, StringComparison.OrdinalIgnoreCase) == 0))
            {
                offset = 0;
            }
            else if (s[i] is '+' or '-' && TryDigits(s, i + 1, 2, out var offsetHours))
            {
                var offsetMinutes = 0;

                if (!(rest == 3 || (rest == 5 && TryDigits(s, i + 3, 2, out offsetMinutes)) || (rest == 6 && s[i + 3] == ':' && TryDigits(s, i + 4, 2, out offsetMinutes)))
                    || offsetMinutes > 59 || offsetHours * 60 + offsetMinutes > 14 * 60)
                {
                    return false;
                }

                offset = (s[i] == '-' ? -1 : 1) * (offsetHours * 60 + offsetMinutes);
            }
            else
            {
                return false;
            }
        }

        var dateTime = new DateTime(year, month, day, hour, minute, second).AddTicks(ticks);

        try
        {
            result = offset is int minutes
                ? new DateTimeOffset(dateTime, TimeSpan.FromMinutes(minutes))
                : new DateTimeOffset(dateTime, context.TimeZone.GetUtcOffset(dateTime));

            return true;
        }
        catch (ArgumentOutOfRangeException)
        {
            // Before year 1 or after year 9999 in UTC.
            return false;
        }
    }

    private static bool TryDigits(string s, int start, int count, out int value)
    {
        value = 0;

        if (start + count > s.Length)
        {
            return false;
        }

        for (var i = start; i < start + count; i++)
        {
            if (!IsDigit(s[i]))
            {
                return false;
            }

            value = value * 10 + s[i] - '0';
        }

        return true;
    }

    private static bool Substitute(ref string str, Regex regex, out Match match)
    {
        match = regex.Match(str);

        if (!match.Success)
        {
            return false;
        }

#if NET8_0_OR_GREATER
        str = string.Concat(str.AsSpan(0, match.Index), " ", str.AsSpan(match.Index + match.Length));
#else
        str = str.Remove(match.Index, match.Length).Insert(match.Index, " ");
#endif
        return true;
    }

    private static string Group(Match match, int index)
    {
        var group = match.Groups[index];
        return group.Success ? group.Value : null;
    }

    private static void ParseTime(ref string str, ref Fields fields)
    {
        if (!Substitute(ref str, TimeRegex(), out var match))
        {
            return;
        }

        fields.Zone = Group(match, 2);

        var time = TimeFieldsRegex().Match(match.Groups[1].Value);

        if (!time.Success)
        {
            return;
        }

        var hour = ToNumber(time.Groups[1].Value);

        if (time.Groups[5].Success)
        {
            // Ruby converts the hour to a 32-bit integer to apply am or pm, and fails if it doesn't fit.
            if (hour > int.MaxValue)
            {
                fields.Invalid = true;
                return;
            }

            hour = hour % 12 + (time.Groups[5].Value[0] == 'p' ? 12 : 0);
        }

        fields.Hour = hour;

        if (time.Groups[2].Success)
        {
            fields.Min = ToNumber(time.Groups[2].Value);
        }

        if (time.Groups[3].Success)
        {
            fields.Sec = ToNumber(time.Groups[3].Value);
        }

        if (time.Groups[4].Success)
        {
            fields.FractionTicks = ToTicks(time.Groups[4].Value);
        }
    }

    private static bool ParseEu(ref string str, ref Fields fields)
    {
        if (!Substitute(ref str, EuRegex(), out var match))
        {
            return false;
        }

        SetDate(ref fields, Group(match, 4), MonthNumber(match.Groups[2].Value), match.Groups[1].Value, IsBc(match.Groups[3]));
        return true;
    }

    private static bool ParseUs(ref string str, ref Fields fields)
    {
        if (!Substitute(ref str, UsRegex(), out var match))
        {
            return false;
        }

        SetDate(ref fields, Group(match, 4), MonthNumber(match.Groups[1].Value), match.Groups[2].Value, IsBc(match.Groups[3]));
        return true;
    }

    private static bool ParseIso(ref string str, ref Fields fields)
    {
        if (!Substitute(ref str, IsoRegex(), out var match))
        {
            return false;
        }

        SetDate(ref fields, match.Groups[1].Value, match.Groups[2].Value, match.Groups[3].Value, bc: false);
        return true;
    }

    // Japanese era names: Meiji, Taisho, Showa, Heisei, Reiwa.
    private static bool ParseJis(ref string str, ref Fields fields)
    {
        if (!Substitute(ref str, JisRegex(), out var match))
        {
            return false;
        }

        var era = match.Groups[1].Value[0] switch
        {
            'm' => 1867,
            't' => 1911,
            's' => 1925,
            'h' => 1988,
            _ => 2018,
        };

        fields.Year = ToNumber(match.Groups[2].Value) + era;
        fields.Mon = ToNumber(match.Groups[3].Value);
        fields.MDay = ToNumber(match.Groups[4].Value);
        return true;
    }

    private static bool ParseVms(ref string str, ref Fields fields)
    {
        if (Substitute(ref str, Vms1Regex(), out var match))
        {
            SetDate(ref fields, match.Groups[3].Value, MonthNumber(match.Groups[2].Value), match.Groups[1].Value, bc: false);
            return true;
        }

        if (Substitute(ref str, Vms2Regex(), out match))
        {
            SetDate(ref fields, Group(match, 3), MonthNumber(match.Groups[1].Value), match.Groups[2].Value, bc: false);
            return true;
        }

        return false;
    }

    private static bool ParseSlashOrDot(ref string str, ref Fields fields, Regex regex)
    {
        if (!Substitute(ref str, regex, out var match))
        {
            return false;
        }

        SetDate(ref fields, match.Groups[1].Value, match.Groups[2].Value, Group(match, 3), bc: false);
        return true;
    }

    private static bool ParseIso2(ref string str, ref Fields fields)
    {
        // Week dates set fields that Time.parse doesn't use.
        if (Substitute(ref str, IsoWeekRegex(), out var match) || Substitute(ref str, IsoWeekDayRegex(), out match))
        {
            return true;
        }

        // --mm-dd or ---dd
        if (Substitute(ref str, IsoMonthDayRegex(), out match))
        {
            if (match.Groups[1].Success)
            {
                fields.Mon = ToNumber(match.Groups[1].Value);
            }

            fields.MDay = ToNumber(match.Groups[2].Value);
            return true;
        }

        // --mmdd or --mm
        if (Substitute(ref str, IsoMonthDay2Regex(), out match))
        {
            fields.Mon = ToNumber(match.Groups[1].Value);

            if (match.Groups[2].Success)
            {
                fields.MDay = ToNumber(match.Groups[2].Value);
            }

            return true;
        }

        if (!IsoOrdinalGuardRegex().IsMatch(str) && Substitute(ref str, IsoOrdinalRegex(), out match))
        {
            fields.Year = ToNumber(match.Groups[1].Value);
            fields.YDay = ToNumber(match.Groups[2].Value);
            return true;
        }

        if (!IsoYearDayGuardRegex().IsMatch(str) && Substitute(ref str, IsoYearDayRegex(), out match))
        {
            fields.YDay = ToNumber(match.Groups[1].Value);
            return true;
        }

        return false;
    }

    private static bool ParseSingle(ref string str, Regex regex, ref long? field)
    {
        if (!Substitute(ref str, regex, out var match))
        {
            return false;
        }

        field = ToNumber(match.Groups[1].Value);
        return true;
    }

    private static bool ParseMonth(ref string str, ref Fields fields)
    {
        if (!Substitute(ref str, MonthRegex(), out var match))
        {
            return false;
        }

        fields.Mon = MonthIndex(match.Groups[1].Value);
        return true;
    }

    // Runs of digits, like 20200615T120000Z.
    private static bool ParseDigits(ref string str, ref Fields fields)
    {
        if (!Substitute(ref str, DigitsRegex(), out var match))
        {
            return false;
        }

        var sign = match.Groups[1].Value;
        var digits = match.Groups[2].Value;
        var time = Group(match, 3);
        var fraction = Group(match, 4);
        var zone = Group(match, 5);
        var length = digits.Length;

        // Only digits and a fraction: the digits end with the seconds.
        var isTime = time == null && fraction != null;

        long Part(int start, int count) => ToNumber(digits.Substring(start, count));
        long Year(int count) => sign == "-" ? -Part(0, count) : Part(0, count);

        switch (length)
        {
            case 2:
                if (isTime)
                {
                    fields.Sec = Part(0, 2);
                }
                else
                {
                    fields.MDay = Part(0, 2);
                }

                break;

            case 3:
                if (isTime)
                {
                    fields.Sec = Part(1, 2);
                    fields.Min = Part(0, 1);
                }
                else
                {
                    fields.YDay = Part(0, 3);
                }

                break;

            case 4:
                if (isTime)
                {
                    fields.Sec = Part(2, 2);
                    fields.Min = Part(0, 2);
                }
                else
                {
                    fields.Mon = Part(0, 2);
                    fields.MDay = Part(2, 2);
                }

                break;

            case 5:
                if (isTime)
                {
                    fields.Sec = Part(3, 2);
                    fields.Min = Part(1, 2);
                    fields.Hour = Part(0, 1);
                }
                else
                {
                    fields.Year = Year(2);
                    fields.YDay = Part(2, 3);
                }

                break;

            case 6:
                if (isTime)
                {
                    fields.Sec = Part(4, 2);
                    fields.Min = Part(2, 2);
                    fields.Hour = Part(0, 2);
                }
                else
                {
                    fields.Year = Year(2);
                    fields.Mon = Part(2, 2);
                    fields.MDay = Part(4, 2);
                }

                break;

            case 7:
                if (isTime)
                {
                    fields.Sec = Part(5, 2);
                    fields.Min = Part(3, 2);
                    fields.Hour = Part(1, 2);
                    fields.MDay = Part(0, 1);
                }
                else
                {
                    fields.Year = Year(4);
                    fields.YDay = Part(4, 3);
                }

                break;

            case 8:
            case 10:
            case 12:
            case 14:
                if (isTime)
                {
                    fields.Sec = Part(length - 2, 2);
                    fields.Min = Part(length - 4, 2);
                    fields.Hour = Part(length - 6, 2);
                    fields.MDay = Part(length - 8, 2);

                    if (length >= 10)
                    {
                        fields.Mon = Part(length - 10, 2);
                    }

                    if (length == 12)
                    {
                        fields.Year = Year(2);
                    }

                    if (length == 14)
                    {
                        fields.Year = Year(4);
                        fields.CompleteYear = false;
                    }
                }
                else
                {
                    fields.Year = Year(4);
                    fields.Mon = Part(4, 2);
                    fields.MDay = Part(6, 2);

                    if (length >= 10)
                    {
                        fields.Hour = Part(8, 2);
                    }

                    if (length >= 12)
                    {
                        fields.Min = Part(10, 2);
                    }

                    if (length >= 14)
                    {
                        fields.Sec = Part(12, 2);
                    }

                    fields.CompleteYear = false;
                }

                break;
        }

        if (time is { Length: 2 or 4 or 6 })
        {
            var t = time.Length;

            if (fraction != null)
            {
                fields.Sec = ToNumber(time.Substring(t - 2, 2));

                if (t >= 4)
                {
                    fields.Min = ToNumber(time.Substring(t - 4, 2));
                }

                if (t >= 6)
                {
                    fields.Hour = ToNumber(time.Substring(t - 6, 2));
                }
            }
            else
            {
                fields.Hour = ToNumber(time.Substring(0, 2));

                if (t >= 4)
                {
                    fields.Min = ToNumber(time.Substring(2, 2));
                }

                if (t >= 6)
                {
                    fields.Sec = ToNumber(time.Substring(4, 2));
                }
            }
        }

        if (fraction != null)
        {
            fields.FractionTicks = ToTicks(fraction);
        }

        if (zone != null)
        {
            if (zone[0] == '[')
            {
                // [-5:EST] is a zone named EST, [+9] a zone named +9.
                zone = zone.Substring(1, zone.Length - 2);
                var colon = zone.IndexOf(':');

                if (colon >= 0)
                {
                    zone = zone.Substring(colon + 1);
                }
            }

            fields.Zone = zone;
        }

        return true;
    }

    // A day or an hour alone, after a time or a date.
    private static void ParseFragment(string str, ref Fields fields)
    {
        var match = FragmentRegex().Match(str);

        if (!match.Success)
        {
            return;
        }

        var n = ToNumber(match.Groups[1].Value);

        if (fields.Hour != null && fields.MDay == null && n is >= 1 and <= 31)
        {
            fields.MDay = n;
        }

        if (fields.MDay != null && fields.Hour == null && n is >= 0 and <= 24)
        {
            fields.Hour = n;
        }
    }

    private static bool IsBc(Group era) => era.Success && era.Value[0] == 'b';

    private static int MonthIndex(string month) => Months.IndexOf(month, StringComparison.Ordinal) / 4 + 1;

    private static string MonthNumber(string month) => MonthIndex(month).ToString(System.Globalization.CultureInfo.InvariantCulture);

    // Assigns the year, month and day matched by a pattern, guessing their order (s3e in date_parse.c).
    private static void SetDate(ref Fields fields, string y, string m, string d, bool bc)
    {
        if (y != null && m != null && d == null)
        {
            (y, m, d) = (d, y, m);
        }

        if (y == null)
        {
            if (d != null && d.Length > 2)
            {
                y = d;
                d = null;
            }

            if (d != null && d.Length > 0 && d[0] == '\'')
            {
                y = d;
                d = null;
            }
        }

        if (y != null)
        {
            var s = SkipToNumber(y, sign: true);

            if (s < y.Length)
            {
                var start = s;

                if (IsSign(y[s]))
                {
                    s++;
                }

                // Text after the digits, like 15th: it's the day.
                var end = s + DigitSpan(y, s);

                if (end < y.Length)
                {
                    var day = y.Substring(start, end - start);
                    y = d;
                    d = day;
                }
            }
        }

        if (m != null && m.Length > 0 && (m[0] == '\'' || m.Length > 2))
        {
            (y, m, d) = (m, d, y);
        }

        if (d != null && d.Length > 0 && (d[0] == '\'' || d.Length > 2))
        {
            (y, d) = (d, y);
        }

        if (y != null)
        {
            var s = SkipToNumber(y, sign: true);

            if (s < y.Length)
            {
                var start = s;

                if (IsSign(y[s]))
                {
                    s++;
                    fields.CompleteYear = false;
                }

                var length = DigitSpan(y, s);

                if (length > 2)
                {
                    fields.CompleteYear = false;
                }

                fields.Year = ToNumber(y.Substring(start, s + length - start));
            }
        }

        if (bc)
        {
            fields.Bc = true;
        }

        if (m != null)
        {
            var s = SkipToNumber(m, sign: false);

            if (s < m.Length)
            {
                fields.Mon = ToNumber(m.Substring(s, DigitSpan(m, s)));
            }
        }

        if (d != null)
        {
            var s = SkipToNumber(d, sign: false);

            if (s < d.Length)
            {
                fields.MDay = ToNumber(d.Substring(s, DigitSpan(d, s)));
            }
        }
    }

    private static bool IsSign(char c) => c is '-' or '+';

    private static bool IsDigit(char c) => (uint)(c - '0') <= 9;

    private static int SkipToNumber(string s, bool sign)
    {
        var i = 0;

        while (i < s.Length && !IsDigit(s[i]) && !(sign && IsSign(s[i])))
        {
            i++;
        }

        return i;
    }

    private static int DigitSpan(string s, int start)
    {
        var i = start;

        while (i < s.Length && IsDigit(s[i]))
        {
            i++;
        }

        return i - start;
    }

    private static long ToNumber(string s)
    {
        var i = 0;
        var negative = false;

        if (i < s.Length && IsSign(s[i]))
        {
            negative = s[i] == '-';
            i++;
        }

        long n = 0;

        for (; i < s.Length && IsDigit(s[i]); i++)
        {
            n = Math.Min(n * 10 + (s[i] - '0'), MaxNumber);
        }

        return negative ? -n : n;
    }

    // Ruby keeps the exact fraction, .NET has a precision of 100 nanoseconds.
    private static long ToTicks(string fraction) => ToTicks(fraction, 0, fraction.Length);

    private static long ToTicks(string s, int start, int count)
    {
        long ticks = 0;

        for (var i = 0; i < 7; i++)
        {
            ticks = ticks * 10 + (i < count ? s[start + i] - '0' : 0);
        }

        return ticks;
    }

    // Time.zone_offset: numeric offsets and the zones Ruby knows, in seconds.
    private static int? ZoneOffset(string zone)
    {
        if (zone.Length > 0 && IsSign(zone[0]))
        {
            var sign = zone[0] == '-' ? -1 : 1;

            bool Digits(int start) => start + 1 < zone.Length && IsDigit(zone[start]) && IsDigit(zone[start + 1]);
            int Two(int start) => (zone[start] - '0') * 10 + zone[start + 1] - '0';

            switch (zone.Length)
            {
                case 3 when Digits(1):
                    return sign * Two(1) * 3600;
                case 5 when Digits(1) && Digits(3):
                    return sign * (Two(1) * 3600 + Two(3) * 60);
                case 6 when Digits(1) && zone[3] == ':' && Digits(4):
                    return sign * (Two(1) * 3600 + Two(4) * 60);
                case 7 when Digits(1) && Digits(3) && Digits(5):
                    return sign * (Two(1) * 3600 + Two(3) * 60 + Two(5));
                case 9 when Digits(1) && zone[3] == ':' && Digits(4) && zone[6] == ':' && Digits(7):
                    return sign * (Two(1) * 3600 + Two(4) * 60 + Two(7));
                default:
                    return null;
            }
        }

        int? hours = zone switch
        {
            "utc" or "z" or "ut" or "gmt" => 0,
            "est" => -5,
            "edt" => -4,
            "cst" => -6,
            "cdt" => -5,
            "mst" => -7,
            "mdt" => -6,
            "pst" => -8,
            "pdt" => -7,
            // Military zones, as defined by RFC 822.
            { Length: 1 } when zone[0] is >= 'a' and <= 'i' => zone[0] - 'a' + 1,
            { Length: 1 } when zone[0] is >= 'k' and <= 'm' => zone[0] - 'k' + 10,
            { Length: 1 } when zone[0] is >= 'n' and <= 'y' => -(zone[0] - 'n' + 1),
            _ => null,
        };

        return hours * 3600;
    }

    // Time.make_time: completes the missing fields from the current time, and validates them.
    private static bool MakeTime(ref Fields fields, TemplateContext context, out DateTimeOffset result)
    {
        result = default;

        if (fields.Year == null && fields.YDay == null && fields.Mon == null && fields.MDay == null
            && fields.Hour == null && fields.Min == null && fields.Sec == null && fields.FractionTicks == null)
        {
            return false;
        }

        var offset = fields.Zone != null ? ZoneOffset(fields.Zone) : null;

        // Ruby's limit for utc_offset.
        if (offset is <= -86400 or >= 86400)
        {
            return false;
        }

        var now = context.Now();
        var localNow = offset is int nowOffset
            ? now.UtcDateTime.AddSeconds(nowOffset)
            : TimeZoneInfo.ConvertTime(now, context.TimeZone).DateTime;

        var year = fields.Year;
        var mon = fields.Mon;
        var day = fields.MDay;
        var hour = fields.Hour;
        var min = fields.Min;
        var sec = fields.Sec;
        var ticks = fields.FractionTicks;

        if (fields.YDay is long yday)
        {
            // Like Time.make_time: guess the month and the day, then move the day by the difference with the day
            // of the year of the result, which also accounts for an hour of 24 or 60 seconds.
            if (yday is < 1 or > 366)
            {
                return false;
            }

            var ordinalYear = year ?? localNow.Year;
            var ordinalMon = (yday - 1) / 31 + 1;
            var ordinalDay = (yday - 1) % 31 + 1;

            if (!TryCompose(ordinalYear, ordinalMon, ordinalDay, hour ?? 0, min ?? 0, sec ?? 0, ticks ?? 0, offset, context, out result, out var localDayOfYear))
            {
                return false;
            }

            var difference = yday - localDayOfYear;

            if (difference == 0)
            {
                return true;
            }

            ordinalDay += difference;

            if (ordinalDay > 28 && MonthDays(ordinalYear, ordinalMon) is int monthDays && ordinalDay > monthDays)
            {
                if (++ordinalMon > 12)
                {
                    return false;
                }

                ordinalDay -= monthDays;
            }

            return TryCompose(ordinalYear, ordinalMon, ordinalDay, hour ?? 0, min ?? 0, sec ?? 0, ticks ?? 0, offset, context, out result, out _);
        }

        // The fields before the first one that is set come from the current time.
        do
        {
            if (year != null) break;
            year = localNow.Year;
            if (mon != null) break;
            mon = localNow.Month;
            if (day != null) break;
            day = localNow.Day;
            if (hour != null) break;
            hour = localNow.Hour;
            if (min != null) break;
            min = localNow.Minute;
            if (sec != null) break;
            sec = localNow.Second;
            if (ticks != null) break;
            ticks = localNow.Ticks % TimeSpan.TicksPerSecond / 10 * 10;
        } while (false);

        return TryCompose(year.Value, mon ?? 1, day ?? 1, hour ?? 0, min ?? 0, sec ?? 0, ticks ?? 0, offset, context, out result, out _);
    }

    // Validates the fields like Time.utc and Time.local, and creates the date in the zone of the offset, or in the context's time zone.
    private static bool TryCompose(long y, long m, long d, long h, long mi, long s, long t, int? offset, TemplateContext context, out DateTimeOffset result, out int localDayOfYear)
    {
        result = default;
        localDayOfYear = 0;

        // With an offset, Ruby converts the fields to UTC before validating them, which normalizes some overflows.
        if (offset is int utcOffset)
        {
            ApplyOffset(ref y, ref m, ref d, ref h, ref mi, ref s, utcOffset);
        }

        // A day after the end of the month and 60 seconds overflow in the next month or minute.
        if (y is < 1 or > 9999 || m is < 1 or > 12 || d is < 1 or > 31 || h is < 0 or > 24 || mi is < 0 or > 59 || s is < 0 or > 60
            || (h == 24 && (mi > 0 || s > 0 || t > 0)))
        {
            return false;
        }

        try
        {
            var dateTime = new DateTime((int)y, (int)m, 1).AddTicks(
                (d - 1) * TimeSpan.TicksPerDay + h * TimeSpan.TicksPerHour + mi * TimeSpan.TicksPerMinute + s * TimeSpan.TicksPerSecond + t);

            if (offset is not int seconds)
            {
                // The offset of the time zone at this date, which can vary with daylight saving time.
                result = new DateTimeOffset(dateTime, context.TimeZone.GetUtcOffset(dateTime));
                localDayOfYear = dateTime.DayOfYear;
            }
            else
            {
                var local = dateTime.AddSeconds(seconds);
                localDayOfYear = local.DayOfYear;

                // .NET offsets are in minutes: like Ruby's %z, ignore the seconds but keep the local time.
                // Beyond the 14 hours .NET supports, keep the instant.
                result = Math.Abs(seconds / 60) <= 14 * 60
                    ? new DateTimeOffset(local, TimeSpan.FromMinutes(seconds / 60))
                    : new DateTimeOffset(dateTime, TimeSpan.Zero);
            }

            return true;
        }
        catch (ArgumentOutOfRangeException)
        {
            return false;
        }
    }

    // Time.apply_offset: converts local fields to UTC, carrying only the units the offset has.
    private static void ApplyOffset(ref long year, ref long mon, ref long day, ref long hour, ref long min, ref long sec, long off)
    {
        var sign = off < 0 ? 1 : -1;
        off = Math.Abs(off);

        if (off == 0)
        {
            return;
        }

        var o = off % 60;
        off /= 60;

        if (o != 0)
        {
            sec += sign * o;
            off += sign * FloorDiv(ref sec, 60);
        }

        o = off % 60;
        off /= 60;

        if (o != 0)
        {
            min += sign * o;
            off += sign * FloorDiv(ref min, 60);
        }

        o = off % 24;
        off /= 24;

        if (o != 0)
        {
            hour += sign * o;
            off += sign * FloorDiv(ref hour, 24);
        }

        if (off == 0)
        {
            return;
        }

        if (sign > 0)
        {
            day += off;

            if (MonthDays(year, mon) is int days && days < day)
            {
                if (++mon > 12)
                {
                    mon = 1;
                    year++;
                }

                day = 1;
            }
        }
        else
        {
            day -= off;

            if (day < 1)
            {
                if (--mon < 1)
                {
                    year--;
                    mon = 12;
                }

                day = MonthDays(year, mon) ?? 0;
            }
        }
    }

    // Returns value / divisor rounded down, and sets value to the remainder, like Ruby's divmod.
    private static long FloorDiv(ref long value, long divisor)
    {
        var quotient = value / divisor;

        if (value % divisor < 0)
        {
            quotient--;
        }

        value -= quotient * divisor;
        return quotient;
    }

    // Ruby indexes an array of month lengths, so 0 is December and 13 is nil.
    private static int? MonthDays(long year, long mon)
    {
        if (mon is < -11 or > 12)
        {
            return null;
        }

        var month = mon < 1 ? mon + 12 : mon;
        var leap = (year % 4 == 0 && year % 100 != 0) || year % 400 == 0;

        return month == 2 ? (leap ? 29 : 28) : month is 4 or 6 or 9 or 11 ? 30 : 31;
    }
}
