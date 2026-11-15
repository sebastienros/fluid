using System;
using System.Threading.Tasks;
using Fluid.Values;
using Xunit;

namespace Fluid.Tests;

public class TimeSpanValueTests
{
    private static readonly FluidParser _parser = new();

    private static async Task<string> RenderAsync(string source, TemplateContext context, object value)
    {
        Assert.True(_parser.TryParse(source, out var template, out var error), error);
        context.SetValue("x", value);
        return await template.RenderAsync(context);
    }

    [Theory]
    [InlineData(1, "America/New_York", "01:00 -05:00")]
    [InlineData(13, "America/New_York", "13:00 -05:00")]
    [InlineData(23, "America/New_York", "23:00 -05:00")]
    [InlineData(1, "Europe/Berlin", "01:00 +01:00")]
    [InlineData(13, "Europe/Berlin", "13:00 +01:00")]
    [InlineData(23, "Europe/Berlin", "23:00 +01:00")]
    [InlineData(1, "UTC", "01:00 +00:00")]
    [InlineData(13, "UTC", "13:00 +00:00")]
    [InlineData(23, "UTC", "23:00 +00:00")]
    public async Task FormatDateKeepsTheClockTimeInTheContextTimeZone(int hours, string timeZoneId, string expected)
    {
        // https://github.com/sebastienros/fluid/issues/1306, with the time zone set on the context
        var context = new TemplateContext { TimeZone = TimeZoneInfo.FindSystemTimeZoneById(timeZoneId) };

        Assert.Equal(expected, await RenderAsync("{{ x | format_date: 'HH:mm K' }}", context, TimeSpan.FromHours(hours)));
    }

    [Theory]
    [InlineData(13, "America/New_York", "13:00 -0500")]
    [InlineData(13, "Europe/Berlin", "13:00 +0100")]
    [InlineData(13, "UTC", "13:00 +0000")]
    public async Task DateKeepsTheClockTimeInTheOptionsTimeZone(int hours, string timeZoneId, string expected)
    {
        // https://github.com/sebastienros/fluid/issues/1306, with the time zone set on the options
        var options = new TemplateOptionsBuilder().WithTimeZone(TimeZoneInfo.FindSystemTimeZoneById(timeZoneId)).Build();

        Assert.Equal(expected, await RenderAsync("{{ x | date: '%H:%M %z' }}", new TemplateContext(options), TimeSpan.FromHours(hours)));
    }

    [Fact]
    public async Task ContextTimeZoneIsUsedOverTheOptionsTimeZone()
    {
        // The value is created with the options, the time zone is only applied by the filter.
        var options = new TemplateOptionsBuilder().WithTimeZone(TimeZoneInfo.Utc).Build();
        var context = new TemplateContext(options) { TimeZone = TimeZoneInfo.FindSystemTimeZoneById("America/New_York") };

        Assert.Equal("13:00 -0500", await RenderAsync("{{ x | date: '%H:%M %z' }}", context, TimeSpan.FromHours(13)));
    }

    [Theory]
    [InlineData("Europe/Istanbul", "13:00 +0300")] // +02:00 in 1970
    [InlineData("Australia/Sydney", "13:00 +1000")] // +11:00 in January with daylight saving time
    public async Task UsesTheStandardOffsetOfTheTimeZone(string timeZoneId, string expected)
    {
        var context = new TemplateContext { TimeZone = TimeZoneInfo.FindSystemTimeZoneById(timeZoneId) };

        Assert.Equal(expected, await RenderAsync("{{ x | date: '%H:%M %z' }}", context, TimeSpan.FromHours(13)));
    }

    [Theory]
    [InlineData("{% if date == x %}true{% else %}false{% endif %}")]
    [InlineData("{% if x == date %}true{% else %}false{% endif %}")]
    public async Task TimeSpanIsNotEqualToADate(string source)
    {
        var context = new TemplateContext();
        context.SetValue("date", new DateTimeOffset(1970, 1, 1, 13, 0, 0, TimeSpan.Zero));

        Assert.Equal("false", await RenderAsync(source, context, TimeSpan.FromHours(13)));
    }

    [Theory]
    [InlineData("13:00:00", "13:00:00")]
    [InlineData("1.02:03:04", "1.02:03:04")]
    [InlineData("00:00:01.5", "00:00:01.5000000")]
    [InlineData("-01:30:00", "-01:30:00")]
    public async Task RendersTheTimeSpan(string timeSpan, string expected)
    {
        Assert.Equal(expected, await RenderAsync("{{ x }}", new TemplateContext(), TimeSpan.Parse(timeSpan)));
    }

    [Fact]
    public async Task TimeSpansAreZeroInMathFilters()
    {
        // Like Liquid, where Utils.to_number returns 0 for anything but a number or a string.
        Assert.Equal("0|0|5", await RenderAsync("{{ x | plus: 0 }}|{{ x | divided_by: 60 }}|{{ x | at_least: 5 }}", new TemplateContext(), TimeSpan.FromSeconds(90.5)));
    }

    [Fact]
    public async Task TimeSpanTooLongForADateIsReturnedUnchanged()
    {
        var context = new TemplateContext { TimeZone = TimeZoneInfo.Utc };

        Assert.Equal(TimeSpan.MaxValue.ToString("c"), await RenderAsync("{{ x | date: '%H:%M' }}", context, TimeSpan.MaxValue));
    }

    [Fact]
    public async Task TimeZoneFilterConvertsTheTimeOfDay()
    {
        var context = new TemplateContext { TimeZone = TimeZoneInfo.FindSystemTimeZoneById("America/New_York") };

        Assert.Equal("19:00 +0100", await RenderAsync("{{ x | time_zone: 'Europe/Berlin' | date: '%H:%M %z' }}", context, TimeSpan.FromHours(13)));
    }

    [Fact]
    public async Task SortsByDuration()
    {
        var context = new TemplateContext();
        context.SetValue("x", new[] { TimeSpan.FromHours(26), TimeSpan.FromHours(13), TimeSpan.FromHours(-1) });
        Assert.True(_parser.TryParse("{{ x | sort | join: ',' }}", out var template, out var error), error);

        Assert.Equal("-01:00:00,13:00:00,1.02:00:00", await template.RenderAsync(context));
    }

    [Fact]
    public async Task EqualTimeSpansAreEqual()
    {
        var context = new TemplateContext();
        context.SetValue("y", TimeSpan.FromMinutes(90));

        Assert.Equal("true", await RenderAsync("{% if x == y %}true{% else %}false{% endif %}", context, TimeSpan.FromHours(1.5)));
    }

    [Fact]
    public async Task JsonWritesTheTimeSpan()
    {
        Assert.Equal("\"13:00:00\"", await RenderAsync("{{ x | json }}", new TemplateContext(), TimeSpan.FromHours(13)));
    }

    [Fact]
    public void ToObjectValueIsTheTimeSpan()
    {
        var value = FluidValue.Create(TimeSpan.FromHours(13), TemplateOptions.Default);

        Assert.Equal(FluidValues.DateTime, value.Type);
        Assert.Equal(TimeSpan.FromHours(13), value.ToObjectValue(new TemplateContext()));
    }
}
