namespace Fluid.Utils;

/// <summary>
/// The abbreviations of time zones, like <c>EST</c>, <c>CEST</c> or <c>+04</c>, which Ruby's <c>%Z</c> shows. .NET doesn't
/// expose them: with ICU, <see cref="TimeZoneInfo.StandardName"/> is <c>Eastern Standard Time</c>. They come from the
/// system's time zone database, the TZif files (RFC 8536) Ruby's <c>localtime</c> reads, in <c>TZDIR</c> or
/// <c>/usr/share/zoneinfo</c>. Windows has no such files, and neither do custom time zones.
/// </summary>
internal static class TimeZoneAbbreviations
{
    // A power of two, so that a mask maps a hash to a slot. Time zones can come from template data through the
    // time_zone filter and a custom resolver, so the cache has a fixed size: an id replaces whichever id held its slot.
    private const int CacheSize = 64;

    // Larger than any TZif file of the time zone database, which are a few kilobytes.
    private const int MaxFileLength = 256 * 1024;

    private static readonly string[] Directories = ["/usr/share/zoneinfo", "/usr/lib/zoneinfo", "/usr/share/lib/zoneinfo"];

    private static readonly Entry[] _cache = new Entry[CacheSize];

    // A class, so that a slot changes in a single write and a reader always gets an id with its own data.
    private sealed class Entry(string id, ZoneData data)
    {
        public string Id { get; } = id;
        public ZoneData Data { get; } = data;
    }

    /// <summary>
    /// Returns the abbreviation of <paramref name="timeZone"/> at the instant of <paramref name="value"/>, when the time
    /// zone database has one for this offset.
    /// </summary>
    public static bool TryGet(TimeZoneInfo timeZone, DateTimeOffset value, out string abbreviation)
    {
        abbreviation = null;
        return GetData(timeZone) is { } data && data.TryGet(timeZone, value, out abbreviation);
    }

    private static ZoneData GetData(TimeZoneInfo timeZone)
    {
        var id = timeZone.Id;
        var slot = StringComparer.Ordinal.GetHashCode(id) & (CacheSize - 1);

        if (_cache[slot] is { } entry && string.Equals(entry.Id, id, StringComparison.Ordinal))
        {
            return entry.Data;
        }

        // Cached as well when there is no file, so that the file system is only looked up once.
        var data = Load(timeZone);
        _cache[slot] = new Entry(id, data);
        return data;
    }

    private static ZoneData Load(TimeZoneInfo timeZone)
    {
        var id = timeZone.Id;

#if NET6_0_OR_GREATER
        // A Windows id, like "Eastern Standard Time", has an IANA equivalent.
        if (!timeZone.HasIanaId && TimeZoneInfo.TryConvertWindowsIdToIanaId(id, out var ianaId))
        {
            id = ianaId;
        }
#endif

        if (!IsIanaId(id))
        {
            return null;
        }

        var tzdir = Environment.GetEnvironmentVariable("TZDIR");
        var directories = string.IsNullOrEmpty(tzdir) ? Directories : [tzdir];

        foreach (var directory in directories)
        {
            try
            {
                var path = Path.Combine(directory, id);
                var file = new FileInfo(path);

                if (!file.Exists || file.Length > MaxFileLength)
                {
                    continue;
                }

                return ZoneData.Parse(File.ReadAllBytes(path));
            }
            catch (Exception e) when (e is IOException or UnauthorizedAccessException or NotSupportedException or ArgumentException or System.Security.SecurityException)
            {
                // Like a missing file: the offset is shown instead.
            }
        }

        return null;
    }

    // Like "America/New_York" or "Etc/GMT+5": a relative path in the database, never one that leaves it.
    private static bool IsIanaId(string id)
    {
        if (id.Length is 0 or > 64 || id[0] is '/' or '.')
        {
            return false;
        }

        foreach (var c in id)
        {
            if (!(c is >= 'a' and <= 'z' or >= 'A' and <= 'Z' or >= '0' and <= '9' or '_' or '-' or '+' or '/'))
            {
                return false;
            }
        }

        return !id.Contains("/.") && !id.Contains("//");
    }

    private sealed class ZoneData
    {
        private readonly long[] _transitions;
        private readonly byte[] _transitionTypes;
        private readonly int[] _offsets;
        private readonly string[] _abbreviations;

        // The POSIX TZ string of the footer, for the instants after the last transition: "EST5EDT,M3.2.0,M11.1.0".
        private readonly string _standard;
        private readonly int _standardOffset;
        private readonly string _daylight;
        private readonly int _daylightOffset;

        private ZoneData(long[] transitions, byte[] transitionTypes, int[] offsets, string[] abbreviations, string footer)
        {
            _transitions = transitions;
            _transitionTypes = transitionTypes;
            _offsets = offsets;
            _abbreviations = abbreviations;

            // Without a footer, or with one that can't be read, the last transition applies after it.
            if (footer == null || !TryParseFooter(footer, out _standard, out _standardOffset, out _daylight, out _daylightOffset))
            {
                _standard = null;
                _daylight = null;
            }
        }

        public bool TryGet(TimeZoneInfo timeZone, DateTimeOffset value, out string abbreviation)
        {
            abbreviation = null;

            var instant = value.ToUnixTimeSeconds();
            var offset = (int)(value.Offset.Ticks / TimeSpan.TicksPerSecond);

            if (_transitions.Length > 0 && instant >= _transitions[_transitions.Length - 1] && _standard != null)
            {
                // After the last transition, the rule of the footer applies. The offset tells standard from daylight
                // time, and .NET's rules tell them apart when they are equal.
                if (_daylight != null && Matches(_daylightOffset, offset) && (!Matches(_standardOffset, offset) || timeZone.IsDaylightSavingTime(value)))
                {
                    abbreviation = _daylight;
                }
                else if (Matches(_standardOffset, offset))
                {
                    abbreviation = _standard;
                }

                return abbreviation != null;
            }

            int type;

            if (_transitions.Length == 0 || instant < _transitions[0])
            {
                // Before the first transition, the first type applies, like in RFC 8536.
                if (_offsets.Length == 0)
                {
                    return false;
                }

                type = 0;
            }
            else
            {
                var index = Array.BinarySearch(_transitions, instant);
                type = _transitionTypes[index >= 0 ? index : ~index - 1];
            }

            // .NET's rules and the database usually agree. When they don't, like for a custom time zone with a
            // system id, the database's abbreviation would be for another offset.
            if (!Matches(_offsets[type], offset))
            {
                return false;
            }

            abbreviation = _abbreviations[type];
            return abbreviation.Length > 0;
        }

        // .NET offsets are whole minutes, while local mean times have seconds: -04:56:02 in New York before 1883.
        private static bool Matches(int seconds, int offset) => Math.Abs(seconds - offset) < 60;

        public static ZoneData Parse(byte[] bytes)
        {
            if (!TryReadHeader(bytes, 0, out var version, out var counts))
            {
                return null;
            }

            var position = 44L;
            var timeSize = 4;

            if (version >= '2')
            {
                // Version 2 and later repeat the data with 64-bit times, and add the footer.
                position += counts.TimeCount * 5L + counts.TypeCount * 6L + counts.CharCount + counts.LeapCount * 8L + counts.StdCount + counts.UtcCount;

                if (!TryReadHeader(bytes, position, out _, out counts))
                {
                    return null;
                }

                position += 44;
                timeSize = 8;
            }

            var dataLength = counts.TimeCount * (timeSize + 1L) + counts.TypeCount * 6L + counts.CharCount;

            if (counts.TypeCount == 0 || position + dataLength > bytes.Length)
            {
                return null;
            }

            var transitions = new long[counts.TimeCount];
            for (var i = 0; i < transitions.Length; i++)
            {
                transitions[i] = timeSize == 8 ? ReadInt64(bytes, position) : ReadInt32(bytes, position);
                position += timeSize;
            }

            var transitionTypes = new byte[counts.TimeCount];
            for (var i = 0; i < transitionTypes.Length; i++)
            {
                transitionTypes[i] = bytes[position++];

                if (transitionTypes[i] >= counts.TypeCount)
                {
                    return null;
                }
            }

            var offsets = new int[counts.TypeCount];
            var designations = new int[counts.TypeCount];
            for (var i = 0; i < offsets.Length; i++)
            {
                offsets[i] = ReadInt32(bytes, position);
                designations[i] = bytes[position + 5];
                position += 6;
            }

            var chars = position;
            var abbreviations = new string[counts.TypeCount];
            for (var i = 0; i < abbreviations.Length; i++)
            {
                abbreviations[i] = ReadString(bytes, chars + designations[i], chars + counts.CharCount, '\0');
            }

            position += counts.CharCount + counts.LeapCount * (timeSize + 4L) + counts.StdCount + counts.UtcCount;

            string footer = null;
            if (timeSize == 8 && position < bytes.Length && bytes[position] == '\n')
            {
                footer = ReadString(bytes, position + 1, bytes.Length, '\n');
            }

            return new ZoneData(transitions, transitionTypes, offsets, abbreviations, footer);
        }

        private struct Counts
        {
            public long UtcCount, StdCount, LeapCount, TimeCount, TypeCount, CharCount;
        }

        private static bool TryReadHeader(byte[] bytes, long position, out byte version, out Counts counts)
        {
            version = 0;
            counts = default;

            if (position < 0 || position + 44 > bytes.Length
                || bytes[position] != 'T' || bytes[position + 1] != 'Z' || bytes[position + 2] != 'i' || bytes[position + 3] != 'f')
            {
                return false;
            }

            version = bytes[position + 4];
            counts.UtcCount = ReadUInt32(bytes, position + 20);
            counts.StdCount = ReadUInt32(bytes, position + 24);
            counts.LeapCount = ReadUInt32(bytes, position + 28);
            counts.TimeCount = ReadUInt32(bytes, position + 32);
            counts.TypeCount = ReadUInt32(bytes, position + 36);
            counts.CharCount = ReadUInt32(bytes, position + 40);

            // Only 256 types can be referenced by a byte, and no count can exceed the file.
            return counts.TypeCount <= 256 && counts.TimeCount <= bytes.Length && counts.CharCount <= bytes.Length
                && counts.LeapCount <= bytes.Length && counts.StdCount <= bytes.Length && counts.UtcCount <= bytes.Length;
        }

        private static uint ReadUInt32(byte[] b, long i) => (uint)(b[i] << 24 | b[i + 1] << 16 | b[i + 2] << 8 | b[i + 3]);

        private static int ReadInt32(byte[] b, long i) => (int)ReadUInt32(b, i);

        private static long ReadInt64(byte[] b, long i) => (long)ReadUInt32(b, i) << 32 | ReadUInt32(b, i + 4);

        private static string ReadString(byte[] bytes, long start, long end, char terminator)
        {
            var length = 0;
            while (start + length < end && start + length < bytes.Length && bytes[start + length] != terminator)
            {
                length++;
            }

            return System.Text.Encoding.ASCII.GetString(bytes, (int)Math.Min(start, bytes.Length), length);
        }

        // std offset [dst [offset] [,rule]], like "CET-1CEST,M3.5.0,M10.5.0/3" or "<+04>-4". POSIX offsets are positive
        // west of Greenwich, and daylight time is an hour ahead when its offset is omitted.
        private static bool TryParseFooter(string footer, out string standard, out int standardOffset, out string daylight, out int daylightOffset)
        {
            standardOffset = 0;
            daylight = null;
            daylightOffset = 0;

            var i = 0;

            if (!TryParseName(footer, ref i, out standard) || !TryParseOffset(footer, ref i, out var posixOffset))
            {
                standard = null;
                return false;
            }

            standardOffset = -posixOffset;

            if (i < footer.Length && footer[i] != ',' && TryParseName(footer, ref i, out daylight))
            {
                daylightOffset = TryParseOffset(footer, ref i, out var posixDaylightOffset) ? -posixDaylightOffset : standardOffset + 3600;
            }

            return true;
        }

        private static bool TryParseName(string s, ref int i, out string name)
        {
            name = null;
            var start = i;

            if (i < s.Length && s[i] == '<')
            {
                var end = s.IndexOf('>', i + 1);
                if (end < 0)
                {
                    return false;
                }

                name = s.Substring(i + 1, end - i - 1);
                i = end + 1;
                return name.Length > 0;
            }

            while (i < s.Length && s[i] is >= 'a' and <= 'z' or >= 'A' and <= 'Z')
            {
                i++;
            }

            name = s.Substring(start, i - start);
            return name.Length > 0;
        }

        // [+-]hh[:mm[:ss]], in seconds.
        private static bool TryParseOffset(string s, ref int i, out int seconds)
        {
            seconds = 0;
            var sign = 1;
            var start = i;

            if (i < s.Length && s[i] is '+' or '-')
            {
                sign = s[i] == '-' ? -1 : 1;
                i++;
            }

            for (var part = 0; part < 3; part++)
            {
                if (part > 0)
                {
                    if (i >= s.Length || s[i] != ':')
                    {
                        break;
                    }

                    i++;
                }

                var value = 0;
                var digits = 0;
                while (i < s.Length && s[i] is >= '0' and <= '9' && digits < 3)
                {
                    value = value * 10 + s[i++] - '0';
                    digits++;
                }

                if (digits == 0)
                {
                    i = start;
                    return false;
                }

                seconds += value * (part == 0 ? 3600 : part == 1 ? 60 : 1);
            }

            seconds *= sign;
            return true;
        }
    }
}
