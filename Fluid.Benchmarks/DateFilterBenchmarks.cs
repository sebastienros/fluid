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

    public DateFilterBenchmarks()
    {
        var parser = new FluidParser();
        _abbreviatedNames = parser.Parse("{{ date | date: '%a, %b %d %Y' }}");
        _fullNames = parser.Parse("{{ date | date: '%A, %B %d %Y' }}");
        _numeric = parser.Parse("{{ date | date: '%Y-%m-%d %H:%M' }}");

        Verify(nameof(AbbreviatedNames), AbbreviatedNames(), "Sat, Mar 09 2024");
        Verify(nameof(FullNames), FullNames(), "Saturday, March 09 2024");
        Verify(nameof(Numeric), Numeric(), "2024-03-09 14:05");

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
    public string Numeric()
    {
        return _numeric.Render(new TemplateContext().SetValue("date", _date));
    }
}
