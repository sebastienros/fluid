using System;
using BenchmarkDotNet.Attributes;
using Fluid.Values;

namespace Fluid.Benchmarks;

/// <summary>
/// The <c>date</c> filter with formats that read day and month names from the culture, next to a
/// purely numeric format that doesn't.
/// </summary>
[MemoryDiagnoser]
public class DateFilterBenchmarks
{
    private readonly FluidValue _date = DateTimeValue.Create(new DateTimeOffset(2024, 3, 9, 14, 5, 7, TimeSpan.Zero));
    private readonly IFluidTemplate _abbreviatedNames;
    private readonly IFluidTemplate _fullNames;
    private readonly IFluidTemplate _numeric;
    private readonly IFluidTemplate _numericWithFlags;
    private readonly IFluidTemplate _parse;

    public DateFilterBenchmarks()
    {
        var parser = new FluidParser();
        _abbreviatedNames = parser.Parse("{{ date | date: '%a, %b %d %Y' }}");
        _fullNames = parser.Parse("{{ date | date: '%A, %B %d %Y' }}");
        _numeric = parser.Parse("{{ date | date: '%Y-%m-%d %H:%M' }}");

        Verify(nameof(AbbreviatedNames), AbbreviatedNames(), "Sat, Mar 09 2024");
        Verify(nameof(FullNames), FullNames(), "Saturday, March 09 2024");
        _numericWithFlags = parser.Parse("{{ date | date: '%-d/%-m/%y %e %l:%M %6Y %j' }}");

        Verify(nameof(Numeric), Numeric(), "2024-03-09 14:05");
        Verify(nameof(NumericWithFlags), NumericWithFlags(), "9/3/24  9  2:05 002024 069");

        _parse = parser.Parse("{{ date | date: '%F %T' }}");

        Verify(nameof(ParseIso8601), ParseIso8601(), "2024-03-09 14:05:07");
        Verify(nameof(ParseWithOffset), ParseWithOffset(), "2024-03-09 14:05:07");
        Verify(nameof(ParseMonthName), ParseMonthName(), "2024-03-09 14:05:07");

        static void Verify(string name, string result, string expected)
        {
            if (result != expected)
            {
                throw new InvalidOperationException($"{name} rendering failed: {result}");
            }
        }
    }

    [Benchmark]
    public string AbbreviatedNames()
    {
        return _abbreviatedNames.Render(new TemplateContext().SetValue("date", _date));
    }

    [Benchmark]
    public string FullNames()
    {
        return _fullNames.Render(new TemplateContext().SetValue("date", _date));
    }

    [Benchmark]
    public string NumericWithFlags()
    {
        return _numericWithFlags.Render(new TemplateContext().SetValue("date", _date));
    }

    [Benchmark]
    public string Numeric()
    {
        return _numeric.Render(new TemplateContext().SetValue("date", _date));
    }

    // Strings parsed like Ruby's Time.parse.

    [Benchmark]
    public string ParseIso8601()
    {
        return _parse.Render(new TemplateContext().SetValue("date", "2024-03-09T14:05:07Z"));
    }

    [Benchmark]
    public string ParseWithOffset()
    {
        return _parse.Render(new TemplateContext().SetValue("date", "2024-03-09 14:05:07 +01:00"));
    }

    [Benchmark]
    public string ParseMonthName()
    {
        return _parse.Render(new TemplateContext().SetValue("date", "March 9, 2024 14:05:07"));
    }
}
