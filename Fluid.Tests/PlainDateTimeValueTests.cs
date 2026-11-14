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
        Assert.Equal("2026-03-04 13:00:00Z", await RenderAsync("{{ x }}", ContextIn("America/New_York"), value));
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
    public async Task RendersTheClockReading()
    {
        var context = ContextIn("America/New_York");

        Assert.Equal("2026-03-04 13:05:06", await RenderAsync("{{ x }}", context, new DateTime(2026, 3, 4, 13, 5, 6, 789)));
        Assert.Equal("0001-01-01 00:00:00", await RenderAsync("{{ x }}", context, default(DateTime)));
        Assert.Equal("2026-03-04 13:05:06", await RenderAsync("{{ x | append: '' }}", context, new DateTime(2026, 3, 4, 13, 5, 6)));
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
    [InlineData("America/New_York", "2026-03-04 00:00 -0500")]
    [InlineData("Europe/Paris", "2026-03-04 00:00 +0100")]
    [InlineData("Asia/Tokyo", "2026-03-04 00:00 +0900")]
    public async Task DateOnlyIsReadAtMidnightInTheContextTimeZone(string timeZoneId, string expected)
    {
        // It used to become the string "03/04/2026", read as April 3.
        Assert.Equal(expected, await RenderAsync("{{ x | date: '%Y-%m-%d %H:%M %z' }}", ContextIn(timeZoneId), new DateOnly(2026, 3, 4)));
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
    [InlineData("Fluid.Accessors.PropertyInfoAccessor", nameof(Schedule.Day), "2026-03-04 00:00 -0500")]
    [InlineData("Fluid.Accessors.ReflectionPropertyInfoAccessor", nameof(Schedule.Day), "2026-03-04 00:00 -0500")]
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
            "2026-03-04 13:00 -0500|2026-03-04 00:00 -0500|2026-01-15 13:00 -0500",
            await RenderAsync("{{ x.StartsProperty | date: '%Y-%m-%d %H:%M %z' }}|{{ x.Day | date: '%Y-%m-%d %H:%M %z' }}|{{ x.Opening | date: '%Y-%m-%d %H:%M %z' }}", context, new Schedule()));
    }
}

[FluidRegister(typeof(PlainDateTimeValueTests.Schedule))]
public partial class ScheduleOptionsBuilder : TemplateOptionsBuilder
{
}
