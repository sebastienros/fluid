using System;
using System.Reflection;
using System.Threading.Tasks;
using Fluid.Accessors;
using Fluid.Values;
using Xunit;

namespace Fluid.Tests;

public class PlainDateTimeValueTests
{
    private static readonly FluidParser _parser = new();

    private static readonly TimeZoneInfo Eastern = TimeZoneInfo.FindSystemTimeZoneById("America/New_York");

    private static async Task<string> RenderAsync(string source, TemplateContext context, object value)
    {
        Assert.True(_parser.TryParse(source, out var template, out var error), error);
        context.SetValue("x", value);
        return await template.RenderAsync(context);
    }

    private static TemplateContext ContextIn(string timeZoneId) => new() { TimeZone = TimeZoneInfo.FindSystemTimeZoneById(timeZoneId) };

    // Whatever the time zone of the machine, at least one of these differs from it.
    [Theory]
    [InlineData("America/New_York", "2026-03-04 13:00 -0500")]
    [InlineData("Europe/Paris", "2026-03-04 13:00 +0100")]
    [InlineData("Asia/Tokyo", "2026-03-04 13:00 +0900")]
    [InlineData("UTC", "2026-03-04 13:00 +0000")]
    public async Task UnspecifiedDateTimeIsReadInTheContextTimeZone(string timeZoneId, string expected)
    {
        var value = new DateTime(2026, 3, 4, 13, 0, 0, DateTimeKind.Unspecified);

        Assert.Equal(expected, await RenderAsync("{{ x | date: '%Y-%m-%d %H:%M %z' }}", ContextIn(timeZoneId), value));
    }

    [Fact]
    public async Task UnspecifiedDateTimeUsesTheOffsetAtItsDate()
    {
        var value = new DateTime(2026, 7, 4, 13, 0, 0, DateTimeKind.Unspecified);

        Assert.Equal("2026-07-04 13:00 -0400", await RenderAsync("{{ x | date: '%Y-%m-%d %H:%M %z' }}", ContextIn("America/New_York"), value));
    }

    // Like Ruby's Time.new, checked with the Liquid gem: a time skipped by daylight saving time is read at the offset
    // before the gap and shown at the offset after it.
    [Theory]
    [InlineData("America/New_York", 2026, 3, 8, 2, 30, "2026-03-08 03:30:00 -0400|2026-03-08 03:30 -0400 1772955000")]
    [InlineData("Europe/Paris", 2026, 3, 29, 2, 30, "2026-03-29 03:30:00 +0200|2026-03-29 03:30 +0200 1774747800")]
    [InlineData("Australia/Sydney", 2026, 10, 4, 2, 30, "2026-10-04 03:30:00 +1100|2026-10-04 03:30 +1100 1791045000")]
    [InlineData("America/New_York", 2026, 11, 1, 1, 30, "2026-11-01 01:30:00 -0500|2026-11-01 01:30 -0500 1793514600")]
    public async Task UnspecifiedDateTimeInADaylightSavingGapIsReadLikeRuby(string timeZoneId, int year, int month, int day, int hour, int minute, string expected)
    {
        var value = new DateTime(year, month, day, hour, minute, 0, DateTimeKind.Unspecified);

        Assert.Equal(expected, await RenderAsync("{{ x }}|{{ x | date: '%Y-%m-%d %H:%M %z %s' }}", ContextIn(timeZoneId), value));
    }

    [Fact]
    public async Task UnspecifiedDateTimeOnTheDaySamoaSkippedIsReadLikeRuby()
    {
        // Read at -10:00, the offset before the gap, like Ruby. Windows' time zone data has no gap on this day, so only
        // the instant is the same everywhere.
        var result = await RenderAsync("{{ x | date: '%Y-%m-%d %H:%M %z %s' }}", ContextIn("Pacific/Apia"), new DateTime(2011, 12, 30, 12, 0, 0));

        Assert.EndsWith(" 1325282400", result);

        if (!OperatingSystem.IsWindows())
        {
            Assert.Equal("2011-12-31 12:00 +1400 1325282400", result);
        }
    }

    [Fact]
    public async Task TimeOnlyInADaylightSavingGapIsReadLikeRuby()
    {
        // Like Time.parse("02:30") on that day.
        var context = ContextIn("America/New_York");
        context.Now = () => new DateTimeOffset(2026, 3, 8, 12, 0, 0, TimeSpan.Zero);

        Assert.Equal("2026-03-08 03:30 -0400", await RenderAsync("{{ x | date: '%Y-%m-%d %H:%M %z' }}", context, new TimeOnly(2, 30)));
    }

    [Fact]
    public async Task UnspecifiedDateTimeIsReadInTheOptionsTimeZone()
    {
        var options = new TemplateOptionsBuilder().WithTimeZone(TimeZoneInfo.FindSystemTimeZoneById("Asia/Tokyo")).Build();
        var value = new DateTime(2026, 3, 4, 13, 0, 0, DateTimeKind.Unspecified);

        Assert.Equal("13:00 +0900", await RenderAsync("{{ x | date: '%H:%M %z' }}", new TemplateContext(options), value));
    }

    [Fact]
    public async Task ContextTimeZoneIsUsedOverTheOptionsTimeZone()
    {
        // The value is created with the options, the time zone is only applied by the filter.
        var options = new TemplateOptionsBuilder().WithTimeZone(TimeZoneInfo.Utc).Build();
        var context = new TemplateContext(options) { TimeZone = Eastern };

        Assert.Equal("13:00 -0500", await RenderAsync("{{ x | date: '%H:%M %z' }}", context, new DateTime(2026, 3, 4, 13, 0, 0)));
    }

    [Fact]
    public async Task UtcDateTimeIsAMoment()
    {
        var value = new DateTime(2026, 3, 4, 13, 0, 0, DateTimeKind.Utc);

        Assert.Equal("2026-03-04 13:00 +0000", await RenderAsync("{{ x | date: '%Y-%m-%d %H:%M %z' }}", ContextIn("America/New_York"), value));
        Assert.Equal("2026-03-04 13:00:00 UTC", await RenderAsync("{{ x }}", ContextIn("America/New_York"), value));
        Assert.IsType<DateTimeValue>(FluidValue.Create(value, TemplateOptions.Default));
    }

    [Fact]
    public async Task LocalDateTimeIsAMomentInTheMachineTimeZone()
    {
        var value = new DateTime(2026, 3, 4, 13, 0, 0, DateTimeKind.Local);
        var offset = TimeZoneInfo.Local.GetUtcOffset(value);
        var expected = $"13:00 {(offset < TimeSpan.Zero ? '-' : '+')}{offset.Duration():hhmm}";

        Assert.Equal(expected, await RenderAsync("{{ x | date: '%H:%M %z' }}", ContextIn("Asia/Tokyo"), value));
        Assert.IsType<DateTimeValue>(FluidValue.Create(value, TemplateOptions.Default));
    }

    [Fact]
    public async Task RendersLikeARubyTimeInTheContextTimeZone()
    {
        // Like Time#to_s, with the offset at the date: daylight saving time in July.
        var context = ContextIn("America/New_York");

        Assert.Equal("2026-03-04 13:05:06 -0500", await RenderAsync("{{ x }}", context, new DateTime(2026, 3, 4, 13, 5, 6, 789)));
        Assert.Equal("2026-07-04 13:05:06 -0400", await RenderAsync("{{ x }}", context, new DateTime(2026, 7, 4, 13, 5, 6)));
        Assert.Equal("2026-03-04 13:05:06 -0500", await RenderAsync("{{ x | append: '' }}", context, new DateTime(2026, 3, 4, 13, 5, 6)));
        Assert.Equal("2026-03-04 13:05:06 +0900", await RenderAsync("{{ x }}", ContextIn("Asia/Tokyo"), new DateTime(2026, 3, 4, 13, 5, 6)));

        // Too close to DateTime.MinValue for the offset of Tokyo, the clock reading is kept in UTC.
        Assert.Equal("0001-01-01 00:00:00 +0000", await RenderAsync("{{ x }}", ContextIn("Asia/Tokyo"), default(DateTime)));
    }

    [Fact]
    public async Task MomentsRenderWithTheirOwnOffset()
    {
        var context = ContextIn("Asia/Tokyo");

        Assert.Equal("2026-03-04 13:00:00 -0500", await RenderAsync("{{ x }}", context, new DateTimeOffset(2026, 3, 4, 13, 0, 0, TimeSpan.FromHours(-5))));
        Assert.Equal("2026-03-04 13:00:00 +0000", await RenderAsync("{{ x }}", context, new DateTimeOffset(2026, 3, 4, 13, 0, 0, TimeSpan.Zero)));
        Assert.Equal("2026-03-04 13:00:00 +0530", await RenderAsync("{{ x }}", context, new DateTimeOffset(2026, 3, 4, 13, 0, 0, TimeSpan.FromMinutes(330))));
        Assert.Equal("0999-01-02 03:04:05 -0130", await RenderAsync("{{ x }}", context, new DateTimeOffset(999, 1, 2, 3, 4, 5, TimeSpan.FromMinutes(-90))));
    }

    public static TheoryData<object> DateValues => new()
    {
        new DateTime(2026, 3, 4, 13, 0, 0, DateTimeKind.Unspecified),
        new DateTime(2026, 3, 4, 13, 0, 0, DateTimeKind.Utc),
        new DateTimeOffset(2026, 3, 4, 13, 0, 0, TimeSpan.FromHours(-5)),
        new DateOnly(2026, 3, 4),
        new TimeOnly(13, 0),
    };

    [Theory]
    [MemberData(nameof(DateValues))]
    public async Task DatesAreZeroInMathFilters(object value)
    {
        // Like Liquid, where Utils.to_number returns 0 for a Time: math is done on date: '%s'.
        Assert.Equal("0|0|0|5|0", await RenderAsync("{{ x | plus: 0 }}|{{ x | times: 2 }}|{{ x | abs }}|{{ x | at_least: 5 }}|{{ x | round }}", ContextIn("America/New_York"), value));
    }

    [Fact]
    public async Task DateMathIsDoneOnUnixTime()
    {
        var context = ContextIn("America/New_York");
        context.SetValue("end", new DateOnly(2026, 12, 25));

        Assert.Equal("2026-03-05 13:00 -0500", await RenderAsync("{{ x | date: '%s' | plus: 86400 | date: '%Y-%m-%d %H:%M %z' }}", context, new DateTime(2026, 3, 4, 13, 0, 0)));
        Assert.Equal("296", await RenderAsync("{% assign start = x | date: '%s' %}{{ end | date: '%s' | minus: start | divided_by: 86400 }}", context, new DateOnly(2026, 3, 4)));
    }

    [Fact]
    public async Task TimeZoneFilterConvertsFromTheContextTimeZone()
    {
        var value = new DateTime(2026, 3, 4, 13, 0, 0, DateTimeKind.Unspecified);

        Assert.Equal("19:00 +0100", await RenderAsync("{{ x | time_zone: 'Europe/Berlin' | date: '%H:%M %z' }}", ContextIn("America/New_York"), value));
    }

    [Theory]
    [InlineData("Europe/Paris")]
    [InlineData("Asia/Tokyo")]
    [InlineData("America/New_York")]
    public async Task DatesTooCloseToTheBoundariesKeepTheirClockReading(string timeZoneId)
    {
        Assert.Equal("0001-01-01 00:00", await RenderAsync("{{ x | date: '%Y-%m-%d %H:%M' }}", ContextIn(timeZoneId), DateTime.MinValue));
        Assert.Equal("9999-12-31 23:59", await RenderAsync("{{ x | date: '%Y-%m-%d %H:%M' }}", ContextIn(timeZoneId), DateTime.MaxValue));
    }

    [Fact]
    public async Task JsonWritesTheDateTimeWithoutOffset()
    {
        Assert.Equal("\"2026-03-04T13:00:00\"", await RenderAsync("{{ x | json }}", ContextIn("America/New_York"), new DateTime(2026, 3, 4, 13, 0, 0)));
    }

    [Fact]
    public async Task EqualDateTimesAreEqual()
    {
        var context = new TemplateContext();
        context.SetValue("y", new DateTime(2026, 3, 4, 13, 0, 0));

        Assert.Equal("true", await RenderAsync("{% if x == y %}true{% else %}false{% endif %}", context, new DateTime(2026, 3, 4, 13, 0, 0)));
    }

    [Fact]
    public async Task DatesOfDifferentKindsAreNotEqual()
    {
        // A DateTimeValue is compared to values of the same FluidValues.DateTime type without a cast.
        var context = new TemplateContext();
        context.SetValue("y", TimeSpan.FromHours(13));
        context.SetValue("z", new DateTime(2026, 3, 4, 13, 0, 0));

        Assert.Equal("false false", await RenderAsync("{% if x == y %}true{% else %}false{% endif %} {% if x == z %}true{% else %}false{% endif %}", context, new DateTimeOffset(2026, 3, 4, 13, 0, 0, TimeSpan.Zero)));
    }

    [Fact]
    public void ToObjectValueIsTheDateTime()
    {
        var value = FluidValue.Create(new DateTime(2026, 3, 4, 13, 0, 0), TemplateOptions.Default);

        Assert.IsType<PlainDateTimeValue>(value);
        Assert.Equal(FluidValues.DateTime, value.Type);
        Assert.Equal(new DateTime(2026, 3, 4, 13, 0, 0), value.ToObjectValue(new TemplateContext()));
    }

    [Theory]
    [InlineData("America/New_York")]
    [InlineData("Europe/Paris")]
    [InlineData("Asia/Tokyo")]
    public async Task DateOnlyIsReadAtMidnightUtcLikeARubyDate(string timeZoneId)
    {
        // It used to become the string "03/04/2026", read as April 3. A Ruby Date has no time zone, strftime reads it in UTC.
        Assert.Equal("2026-03-04 00:00 +0000 1772582400", await RenderAsync("{{ x | date: '%Y-%m-%d %H:%M %z %s' }}", ContextIn(timeZoneId), new DateOnly(2026, 3, 4)));
    }

    [Fact]
    public async Task DateOnlyRendersAsAnIsoDate()
    {
        Assert.Equal("2026-03-04", await RenderAsync("{{ x }}", new TemplateContext(), new DateOnly(2026, 3, 4)));
        Assert.Equal("\"2026-03-04\"", await RenderAsync("{{ x | json }}", new TemplateContext(), new DateOnly(2026, 3, 4)));
    }

    [Fact]
    public async Task DateOnlyFormatsInTheContextCulture()
    {
        var context = ContextIn("America/New_York");
        context.CultureInfo = new System.Globalization.CultureInfo("fr-FR");

        Assert.Equal("04/03/2026", await RenderAsync("{{ x | format_date: 'd' }}", context, new DateOnly(2026, 3, 4)));
    }

    [Fact]
    public async Task DatesSortChronologically()
    {
        var context = new TemplateContext();
        context.SetValue("x", new[] { new DateOnly(2026, 11, 2), new DateOnly(999, 1, 1), new DateOnly(2026, 3, 4) });
        Assert.True(_parser.TryParse("{{ x | sort | join: ',' }}", out var template, out var error), error);

        Assert.Equal("0999-01-01,2026-03-04,2026-11-02", await template.RenderAsync(context));
    }

    [Fact]
    public void ToObjectValueIsTheDateOnly()
    {
        var value = FluidValue.Create(new DateOnly(2026, 3, 4), TemplateOptions.Default);

        Assert.Equal(FluidValues.DateTime, value.Type);
        Assert.Equal(new DateOnly(2026, 3, 4), value.ToObjectValue(new TemplateContext()));
        Assert.Equal(PlainDateTimeValue.Create(new DateOnly(2026, 3, 4)), value);
        Assert.NotEqual(PlainDateTimeValue.Create(new DateTime(2026, 3, 4)), value);
    }

    [Theory]
    [InlineData("America/New_York", "2026-07-15 13:00 -0400")]
    [InlineData("Europe/Paris", "2026-07-15 13:00 +0200")]
    [InlineData("UTC", "2026-07-15 13:00 +0000")]
    public async Task TimeOnlyIsReadTodayInTheContextTimeZone(string timeZoneId, string expected)
    {
        var context = ContextIn(timeZoneId);
        context.Now = () => new DateTimeOffset(2026, 7, 15, 12, 0, 0, TimeSpan.Zero);

        Assert.Equal(expected, await RenderAsync("{{ x | date: '%Y-%m-%d %H:%M %z' }}", context, new TimeOnly(13, 0)));
    }

    [Fact]
    public async Task TimeOnlyIsOnTheCurrentDateOfTheContextTimeZone()
    {
        // 02:00 UTC is still the day before in New York.
        var context = ContextIn("America/New_York");
        context.Now = () => new DateTimeOffset(2026, 7, 15, 2, 0, 0, TimeSpan.Zero);

        Assert.Equal("2026-07-14 13:00 -0400", await RenderAsync("{{ x | date: '%Y-%m-%d %H:%M %z' }}", context, new TimeOnly(13, 0)));
    }

    [Fact]
    public async Task TimeOnlyRendersAsAnIsoTime()
    {
        Assert.Equal("13:05:06", await RenderAsync("{{ x }}", new TemplateContext(), new TimeOnly(13, 5, 6, 789)));
        Assert.Equal("\"13:05:06\"", await RenderAsync("{{ x | json }}", new TemplateContext(), new TimeOnly(13, 5, 6)));
    }

    [Fact]
    public async Task TimeOnlyTimeZoneFilterConvertsTheTimeOfDay()
    {
        var context = ContextIn("America/New_York");
        context.Now = () => new DateTimeOffset(2026, 1, 15, 12, 0, 0, TimeSpan.Zero);

        Assert.Equal("19:00 +0100", await RenderAsync("{{ x | time_zone: 'Europe/Berlin' | date: '%H:%M %z' }}", context, new TimeOnly(13, 0)));
    }

    [Fact]
    public async Task TimesSortChronologically()
    {
        var context = new TemplateContext();
        context.SetValue("x", new[] { new TimeOnly(13, 0), new TimeOnly(9, 30), new TimeOnly(23, 59, 59) });
        Assert.True(_parser.TryParse("{{ x | sort | join: ',' }}", out var template, out var error), error);

        Assert.Equal("09:30:00,13:00:00,23:59:59", await template.RenderAsync(context));
    }

    [Fact]
    public void ToObjectValueIsTheTimeOnly()
    {
        var value = FluidValue.Create(new TimeOnly(13, 0), TemplateOptions.Default);

        Assert.Equal(FluidValues.DateTime, value.Type);
        Assert.Equal(new TimeOnly(13, 0), value.ToObjectValue(new TemplateContext()));
        Assert.True(value.Equals((object)new TimeOnly(13, 0)));
    }

    private static async Task<string> CompareAsync(string source, object a, object b, string timeZoneId = "America/New_York")
    {
        var context = ContextIn(timeZoneId);
        context.SetValue("b", b);
        return await RenderAsync(source, context, a);
    }

    private const string Comparisons = "{% if x < b %}lt{% endif %}{% if x <= b %}le{% endif %}{% if x == b %}eq{% endif %}{% if x >= b %}ge{% endif %}{% if x > b %}gt{% endif %}";

    [Fact]
    public async Task MomentsAreOrderedLikeRubyTimes()
    {
        var march4 = new DateTime(2026, 3, 4, 13, 0, 0);

        Assert.Equal("ltle", await CompareAsync(Comparisons, march4, new DateTime(2026, 3, 5, 13, 0, 0)));
        Assert.Equal("gegt", await CompareAsync(Comparisons, march4, new DateTime(2026, 3, 4, 12, 59, 59)));
        Assert.Equal("leeqge", await CompareAsync(Comparisons, march4, march4));

        // A date and time is read in the time zone of the context: 13:00 in New York is 18:00 UTC.
        Assert.Equal("le|le", await CompareAsync("{% if x < b %}lt{% endif %}{% if x <= b %}le{% endif %}|{% if b <= x %}le{% endif %}{% if b < x %}lt{% endif %}", march4, new DateTimeOffset(2026, 3, 4, 18, 0, 0, TimeSpan.Zero)));
        Assert.Equal("gt", await CompareAsync("{% if x > b %}gt{% endif %}", march4, new DateTimeOffset(2026, 3, 4, 13, 0, 0, TimeSpan.Zero)));

        // Moments in different offsets are compared as instants.
        Assert.Equal("ltle", await CompareAsync(Comparisons, new DateTimeOffset(2026, 3, 4, 13, 0, 0, TimeSpan.Zero), new DateTimeOffset(2026, 3, 4, 9, 0, 0, TimeSpan.FromHours(-5))));
    }

    [Fact]
    public async Task DatesAreOrderedLikeRubyDates()
    {
        Assert.Equal("ltle", await CompareAsync(Comparisons, new DateOnly(2026, 3, 4), new DateOnly(2026, 3, 5)));
        Assert.Equal("leeqge", await CompareAsync(Comparisons, new DateOnly(2026, 3, 4), new DateOnly(2026, 3, 4)));
        Assert.Equal("gegt", await CompareAsync(Comparisons, new DateOnly(2026, 3, 4), new DateOnly(999, 1, 1)));
    }

    [Fact]
    public async Task DatesAreComparedWithNumbersByJulianDay()
    {
        // Like Ruby, which compares the astronomical Julian day of a Date with a number: 2461103.5 for 2026-03-04.
        Assert.Equal("gegt", await CompareAsync(Comparisons, new DateOnly(2026, 3, 4), 5));
        Assert.Equal("le", await CompareAsync("{% if x < b %}lt{% endif %}{% if x <= b %}le{% endif %}", new DateOnly(2026, 3, 4), 2461103.5m));
        Assert.Equal("lt", await CompareAsync("{% if x < b %}lt{% endif %}", new DateOnly(2000, 1, 1), 2451544.6m));
    }

    public static TheoryData<string, object, object, string> FailingComparisons => new()
    {
        { "{% if x < b %}{% endif %}", new DateTime(2026, 3, 4, 13, 0, 0), new DateOnly(2026, 3, 4), "comparison of Time with Date failed" },
        { "{% if x > b %}{% endif %}", new DateOnly(2026, 3, 4), new DateTimeOffset(2026, 3, 4, 13, 0, 0, TimeSpan.Zero), "comparison of Date with Time failed" },
        { "{% if x < b %}{% endif %}", new DateTime(2026, 3, 4, 13, 0, 0), "2026", "comparison of Time with String failed" },
        { "{% if x >= b %}{% endif %}", new DateOnly(2026, 3, 4), "2026", "comparison of Date with String failed" },
        { "{% if b < x %}{% endif %}", new DateTime(2026, 3, 4, 13, 0, 0), "2026", "comparison of String with Time failed" },
        { "{% if x < b %}{% endif %}", new DateTime(2026, 3, 4, 13, 0, 0), 5, "comparison of Time with 5 failed" },
        { "{% if x < b %}{% endif %}", new DateTime(2026, 3, 4, 13, 0, 0), 1.5m, "comparison of Time with 1.5 failed" },
        { "{% if b < x %}{% endif %}", new DateTime(2026, 3, 4, 13, 0, 0), 5, "comparison of Integer with Time failed" },
        { "{% if b <= x %}{% endif %}", new DateTimeOffset(2026, 3, 4, 13, 0, 0, TimeSpan.Zero), 1.5m, "comparison of Float with Time failed" },
        { "{% if b > x %}{% endif %}", new DateOnly(2026, 3, 4), 5, "comparison of Integer with Date failed" },
    };

    [Theory]
    [MemberData(nameof(FailingComparisons))]
    public async Task ComparingADateWithAnotherKindOfValueFails(string source, object a, object b, string message)
    {
        var exception = await Assert.ThrowsAsync<LiquidException>(() => CompareAsync(source, a, b));

        Assert.Equal(message, exception.Message);
    }

    [Fact]
    public async Task OtherComparisonsWithDatesAreFalse()
    {
        // Like nil or a Liquid drop, which aren't Comparable in Ruby: a time of day and a time span have no Ruby equivalent.
        var context = ContextIn("America/New_York");
        context.SetValue("t", new TimeOnly(13, 0));
        context.SetValue("s", TimeSpan.FromHours(13));
        context.SetValue("e", Array.Empty<int>());

        Assert.Equal("false", await RenderAsync("{% if x < nil or x > nil or nil < x or x < t or t < x or x > s or s > x or x < e or e < x %}true{% else %}false{% endif %}", context, new DateTime(2026, 3, 4, 13, 0, 0)));
    }

    [Fact]
    public async Task MomentsSortChronologically()
    {
        var context = ContextIn("America/New_York");
        context.SetValue("x", new object[] { new DateTimeOffset(2026, 3, 4, 4, 0, 0, TimeSpan.FromHours(-5)), new DateTimeOffset(2026, 3, 4, 12, 0, 0, TimeSpan.FromHours(5)), new DateTimeOffset(2026, 3, 4, 8, 0, 0, TimeSpan.Zero) });
        Assert.True(_parser.TryParse("{{ x | sort | join: ',' }}", out var template, out var error), error);

        // 07:00, 08:00 and 09:00 UTC. As text, they would sort the other way around.
        Assert.Equal("2026-03-04 12:00:00 +0500,2026-03-04 08:00:00 +0000,2026-03-04 04:00:00 -0500", await template.RenderAsync(context));
    }

    public sealed class Schedule
    {
        public DateTime StartsProperty { get; set; } = new DateTime(2026, 3, 4, 13, 0, 0);
        public DateTime StartsField = new DateTime(2026, 3, 4, 13, 0, 0);
        public DateOnly Day { get; set; } = new DateOnly(2026, 3, 4);
        public TimeOnly Opening { get; set; } = new TimeOnly(13, 0);
    }

    [Theory]
    [InlineData("Fluid.Accessors.PropertyInfoAccessor", nameof(Schedule.StartsProperty), "2026-03-04 13:00 -0500")]
    [InlineData("Fluid.Accessors.ReflectionPropertyInfoAccessor", nameof(Schedule.StartsProperty), "2026-03-04 13:00 -0500")]
    [InlineData("Fluid.Accessors.FieldInfoAccessor", nameof(Schedule.StartsField), "2026-03-04 13:00 -0500")]
    [InlineData("Fluid.Accessors.ReflectionFieldInfoAccessor", nameof(Schedule.StartsField), "2026-03-04 13:00 -0500")]
    [InlineData("Fluid.Accessors.PropertyInfoAccessor", nameof(Schedule.Day), "2026-03-04 00:00 +0000")]
    [InlineData("Fluid.Accessors.ReflectionPropertyInfoAccessor", nameof(Schedule.Day), "2026-03-04 00:00 +0000")]
    public async Task AccessorsReadMembersInTheContextTimeZone(string accessorTypeName, string memberName, string expected)
    {
        var member = (MemberInfo)typeof(Schedule).GetProperty(memberName) ?? typeof(Schedule).GetField(memberName);
        var accessorType = typeof(PropertyInfoAccessor).Assembly.GetType(accessorTypeName, throwOnError: true);
        var accessor = (MemberAccessor)Activator.CreateInstance(accessorType, member);
        var context = new TemplateContext { TimeZone = Eastern };

        var value = await accessor.GetAsync(new Schedule(), memberName, context);

        Assert.IsType<PlainDateTimeValue>(value);
        Assert.Equal(expected, await RenderAsync("{{ x | date: '%Y-%m-%d %H:%M %z' }}", context, value));
    }

    [Fact]
    public async Task GeneratedAccessorsReadMembersInTheContextTimeZone()
    {
        var options = new ScheduleOptionsBuilder().Build();
        var context = new TemplateContext(options) { TimeZone = Eastern, Now = () => new DateTimeOffset(2026, 1, 15, 12, 0, 0, TimeSpan.Zero) };

        Assert.Equal(
            "2026-03-04 13:00 -0500|2026-03-04 00:00 +0000|2026-01-15 13:00 -0500",
            await RenderAsync("{{ x.StartsProperty | date: '%Y-%m-%d %H:%M %z' }}|{{ x.Day | date: '%Y-%m-%d %H:%M %z' }}|{{ x.Opening | date: '%Y-%m-%d %H:%M %z' }}", context, new Schedule()));
    }
}

[FluidRegister(typeof(PlainDateTimeValueTests.Schedule))]
public partial class ScheduleOptionsBuilder : TemplateOptionsBuilder
{
}
