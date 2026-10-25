using Fluid.Filters;
using Fluid.Values;
using System;
using System.Globalization;
using System.Threading.Tasks;
using Xunit;

namespace Fluid.Tests;

public class DateDirectiveTests
{
    private static async Task<string> FormatAsync(DateTimeOffset date, string format)
    {
        var options = new TemplateOptionsBuilder()
            .WithCultureInfo(CultureInfo.InvariantCulture)
            .WithTimeZone(TimeZoneInfo.Utc)
            .Build();

        var result = await MiscFilters.Date(DateTimeValue.Create(date), new FilterArguments(StringValue.Create(format)), new TemplateContext(options));

        return result.ToStringValue(new TemplateContext());
    }

    private static DateTimeOffset At(int year, int month, int day, int hour = 0, int minute = 0, int second = 0, int millisecond = 0) =>
        new DateTimeOffset(year, month, day, hour, minute, second, millisecond, TimeSpan.Zero);

    [Theory]
    // The '-' flag drops the padding but keeps a zero.
    [InlineData("%-M|%-S|%-H|%-d", "0|0|9|1")]
    [InlineData("%H|%_H|%-H|%4H", "09| 9|9|0009")]
    // %k is blank-padded.
    [InlineData("%k|%-k|%_k", " 9|9| 9")]
    public async Task HoursMinutesAndSeconds(string format, string expected)
    {
        Assert.Equal(expected, await FormatAsync(At(2024, 1, 1, 9, 0, 0), format));
    }

    [Fact]
    public async Task TwoDigitHoursAreUnchanged()
    {
        Assert.Equal("14|14|14|14", await FormatAsync(At(2024, 1, 1, 14, 5, 7), "%H|%k|%-H|%-k"));
    }

    [Theory]
    [InlineData("%N", "005000000")]
    [InlineData("%3N", "005")]
    [InlineData("%6N", "005000")]
    [InlineData("%9N", "005000000")]
    [InlineData("%12N", "005000000000")]
    [InlineData("%L", "005")]
    public async Task FractionalSecondsKeepTheirLeadingZeros(string format, string expected)
    {
        Assert.Equal(expected, await FormatAsync(At(2024, 1, 1, 0, 0, 0, 5), format));
    }

    [Theory]
    // Sunday of ISO week 1.
    [InlineData(2024, 1, 7, "01|2024|24")]
    // Monday of week 1 of the next year.
    [InlineData(2024, 12, 30, "01|2025|25")]
    // Friday of week 53 of the previous year.
    [InlineData(2021, 1, 1, "53|2020|20")]
    [InlineData(2026, 12, 31, "53|2026|26")]
    [InlineData(2005, 1, 10, "02|2005|05")]
    [InlineData(2024, 6, 15, "24|2024|24")]
    public async Task IsoWeekAndYear(int year, int month, int day, string expected)
    {
        Assert.Equal(expected, await FormatAsync(At(year, month, day), "%V|%G|%g"));
    }

    [Fact]
    public async Task IsoWeekMatchesTheFrameworkForEveryDay()
    {
        for (var date = new DateTime(1999, 12, 1); date < new DateTime(2031, 2, 1); date = date.AddDays(1))
        {
            var expected = $"{ISOWeek.GetWeekOfYear(date):00}|{ISOWeek.GetYear(date)}";

            Assert.Equal(expected, await FormatAsync(new DateTimeOffset(date, TimeSpan.Zero), "%V|%G"));
        }
    }
}
