using System;
using System.Threading.Tasks;
using Xunit;

namespace Fluid.Tests;

// %Z like Ruby's Time#strftime, checked with the Liquid gem 5.14.0 with TZ set to the time zone.
public class StrftimeZoneNameTests
{
    private static readonly FluidParser _parser = new();

    // The abbreviations come from the time zone database, which Windows doesn't have: the offset is shown there instead.
    private static string Abbreviation(string abbreviation, string offset) => OperatingSystem.IsWindows() ? offset : abbreviation;

    // The offset .NET gives a local time, which depends on the time zone data of the platform for old dates.
    private static string OffsetOf(string timeZoneId, DateTime dateTime) => new DateTimeOffset(dateTime, TimeZoneInfo.FindSystemTimeZoneById(timeZoneId).GetUtcOffset(dateTime)).ToString("zzz", System.Globalization.CultureInfo.InvariantCulture);

    private static async Task<string> RenderAsync(string source, string timeZoneId, object value = null)
    {
        var context = new TemplateContext { TimeZone = TimeZoneInfo.FindSystemTimeZoneById(timeZoneId) };
        context.SetValue("x", value);
        return await _parser.Parse(source).RenderAsync(context);
    }

    public static TheoryData<string, string, string> LocalTimes => new()
    {
        { "America/New_York", "2026-01-15 12:00:00", Abbreviation("EST", "-05:00") },
        { "America/New_York", "2026-07-15 12:00:00", Abbreviation("EDT", "-04:00") },
        { "America/New_York", "2026-07-15 12:00:00 -0400", Abbreviation("EDT", "-04:00") },
        { "America/New_York", "2026-07-15 12:00:00 EDT", Abbreviation("EDT", "-04:00") },
        { "America/New_York", "1784116800", Abbreviation("EDT", "-04:00") },
        { "Europe/London", "2026-01-15 12:00:00", Abbreviation("GMT", "UTC") },
        { "Europe/London", "2026-07-15 12:00:00", Abbreviation("BST", "+01:00") },
        { "Europe/Dublin", "2026-01-15 12:00:00", Abbreviation("GMT", "UTC") }, // Daylight saving time in winter for tzdata
        { "Europe/Dublin", "2026-07-15 12:00:00", Abbreviation("IST", "+01:00") },
        { "Europe/Paris", "2026-07-15 12:00:00", Abbreviation("CEST", "+02:00") },
        { "Asia/Dubai", "2026-07-15 12:00:00", Abbreviation("+04", "+04:00") },
        { "Pacific/Chatham", "2026-07-15 12:00:00", Abbreviation("+1245", "+12:45") },
        { "Australia/Lord_Howe", "2026-07-15 12:00:00", Abbreviation("+1030", "+10:30") },
        { "America/New_York", "2100-07-15 12:00:00", Abbreviation("EDT", "-04:00") }, // After the transitions of the file
        { "America/New_York", "1850-06-01 12:00:00", Abbreviation("LMT", OffsetOf("America/New_York", new DateTime(1850, 6, 1, 12, 0, 0))) }, // Before the first one
        { "UTC", "2026-07-15 12:00:00", "UTC" },
    };

    [Theory]
    [MemberData(nameof(LocalTimes))]
    public async Task LocalTimesHaveTheAbbreviationOfTheTimeZone(string timeZoneId, string input, string expected)
    {
        Assert.Equal(expected, await RenderAsync("{{ x | date: '%Z' }}", timeZoneId, input));
    }

    [Theory]
    [InlineData("America/New_York", "2026-07-15 12:00:00 +0400")]
    [InlineData("America/New_York", "2026-07-15 12:00:00 +00:00")]
    [InlineData("America/New_York", "2026-07-15 12:00:00 GMT")] // GMT is a zero offset, UTC is a UTC time
    [InlineData("America/New_York", "2026-01-15 12:00:00 EDT")]
    [InlineData("Europe/London", "2026-07-15 12:00:00 +0000")]
    [InlineData("UTC", "2026-07-15 12:00:00 -0400")]
    public async Task TimesWithAnotherOffsetHaveNoName(string timeZoneId, string input)
    {
        Assert.Equal("[]", await RenderAsync("[{{ x | date: '%Z' }}]", timeZoneId, input));
    }

    [Theory]
    [InlineData("2026-07-15T12:00:00Z")]
    [InlineData("2026-07-15 12:00:00 UTC")]
    [InlineData("2026-07-15 12:00:00 UT")]
    [InlineData("2026-07-15 12:00:00 -00:00")]
    [InlineData("July 15, 2026 12:00 UTC")]
    [InlineData("Wed, 15 Jul 2026 12:00:00 -0000")]
    public async Task UtcTimesAreNamedUtc(string input)
    {
        Assert.Equal("UTC +0000", await RenderAsync("{{ x | date: '%Z %z' }}", "America/New_York", input));
    }

    [Fact]
    public async Task ModelValuesAreNamedLikeRubyTimesAndDates()
    {
        // A DateTime of Utc kind is like Time.utc, an Unspecified one like a local Time, and a DateTimeOffset like a Time
        // with an offset. A DateOnly is like a Date.
        Assert.Equal("UTC", await RenderAsync("{{ x | date: '%Z' }}", "America/New_York", new DateTime(2026, 7, 15, 12, 0, 0, DateTimeKind.Utc)));
        Assert.Equal(Abbreviation("EDT", "-04:00"), await RenderAsync("{{ x | date: '%Z' }}", "America/New_York", new DateTime(2026, 7, 15, 12, 0, 0)));
        Assert.Equal(Abbreviation("EDT", "-04:00"), await RenderAsync("{{ x | date: '%Z' }}", "America/New_York", new DateTimeOffset(2026, 7, 15, 12, 0, 0, TimeSpan.FromHours(-4))));
        Assert.Equal("[]", await RenderAsync("[{{ x | date: '%Z' }}]", "America/New_York", new DateTimeOffset(2026, 7, 15, 12, 0, 0, TimeSpan.FromHours(5.5))));
        Assert.Equal("+00:00 +0000", await RenderAsync("{{ x | date: '%Z %z' }}", "America/New_York", new DateOnly(2026, 7, 15)));
    }

    [Fact]
    public async Task FlagsApplyToTheName()
    {
        Assert.Equal(Abbreviation("[EDT|EDT|edt|   EDT|EDT]", "[-04:00|-04:00|-04:00|-04:00|-04:00]"), await RenderAsync("[{{ x | date: '%Z|%^Z|%#Z|%6Z|%-6Z' }}]", "America/New_York", "2026-07-15 12:00:00"));
    }

    [Fact]
    public async Task TimeZoneFilterNamesItsTimeZone()
    {
        Assert.Equal(Abbreviation("JST", "+09:00"), await RenderAsync("{{ x | time_zone: 'Asia/Tokyo' | date: '%Z' }}", "America/New_York", "2026-07-15 12:00:00 UTC"));
        Assert.Equal(Abbreviation("CEST", "+02:00"), await RenderAsync("{{ x | time_zone: 'Europe/Paris' | date: '%Z' }}", "UTC", "2026-07-15 12:00:00"));
        Assert.Equal("UTC", await RenderAsync("{{ x | time_zone: 'UTC' | date: '%Z' }}", "America/New_York", "2026-07-15 12:00:00"));
    }

    [Fact]
    public async Task CustomTimeZonesShowTheirOffset()
    {
        // No database entry: the offset stands for the name, and UTC for a zero offset.
        var context = new TemplateContext { TimeZone = TimeZoneInfo.CreateCustomTimeZone("Custom", TimeSpan.FromHours(2), "Custom", "Custom") };
        Assert.Equal("+02:00", await _parser.Parse("{{ '2026-07-15 12:00:00' | date: '%Z' }}").RenderAsync(context));

        // A custom time zone with the id of a system one, but other offsets, doesn't use its abbreviations.
        context.TimeZone = TimeZoneInfo.CreateCustomTimeZone("America/New_York", TimeSpan.FromHours(3), "Custom", "Custom");
        Assert.Equal("+03:00", await _parser.Parse("{{ '2026-07-15 12:00:00' | date: '%Z' }}").RenderAsync(context));

        // Ids are never paths outside the database.
        context.TimeZone = TimeZoneInfo.CreateCustomTimeZone("../../../../etc/passwd", TimeSpan.Zero, "Custom", "Custom");
        Assert.Equal("UTC", await _parser.Parse("{{ '2026-07-15 12:00:00' | date: '%Z' }}").RenderAsync(context));
    }
}
