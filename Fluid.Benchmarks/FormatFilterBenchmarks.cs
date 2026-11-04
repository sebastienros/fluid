using System;
using BenchmarkDotNet.Attributes;
using Fluid.Values;

namespace Fluid.Benchmarks;

/// <summary>
/// The <c>format_number</c>, <c>format_date</c> and <c>format_string</c> filters with a culture argument,
/// next to <c>format_number</c> with the culture of the context.
/// </summary>
[MemoryDiagnoser]
public class FormatFilterBenchmarks
{
    private readonly FluidValue _number = NumberValue.Create(1234.5m);
    private readonly FluidValue _date = DateTimeValue.Create(new DateTimeOffset(2024, 1, 9, 14, 5, 7, TimeSpan.Zero));
    private readonly IFluidTemplate _formatNumber;
    private readonly IFluidTemplate _formatNumberWithCulture;
    private readonly IFluidTemplate _formatDateWithCulture;
    private readonly IFluidTemplate _formatStringWithCulture;

    public FormatFilterBenchmarks()
    {
        var parser = new FluidParser();
        _formatNumber = parser.Parse("{{ number | format_number: 'N2' }}");
        _formatNumberWithCulture = parser.Parse("{{ number | format_number: 'N2', 'de-DE' }}");
        _formatDateWithCulture = parser.Parse("{{ date | format_date: 'D', 'de-DE' }}");
        _formatStringWithCulture = parser.Parse("{{ '{0:N2}' | format_string: number, culture: 'de-DE' }}");

        Verify(nameof(FormatNumber), FormatNumber(), "1,234.50");
        Verify(nameof(FormatNumberWithCulture), FormatNumberWithCulture(), "1.234,50");
        Verify(nameof(FormatDateWithCulture), FormatDateWithCulture(), "Dienstag, 9. Januar 2024");
        Verify(nameof(FormatStringWithCulture), FormatStringWithCulture(), "1.234,50");

        static void Verify(string name, string result, string expected)
        {
            if (result != expected)
            {
                throw new InvalidOperationException($"{name} rendering failed: {result}");
            }
        }
    }

    [Benchmark]
    public string FormatNumber()
    {
        return _formatNumber.Render(new TemplateContext().SetValue("number", _number));
    }

    [Benchmark]
    public string FormatNumberWithCulture()
    {
        return _formatNumberWithCulture.Render(new TemplateContext().SetValue("number", _number));
    }

    [Benchmark]
    public string FormatDateWithCulture()
    {
        return _formatDateWithCulture.Render(new TemplateContext().SetValue("date", _date));
    }

    [Benchmark]
    public string FormatStringWithCulture()
    {
        return _formatStringWithCulture.Render(new TemplateContext().SetValue("number", _number));
    }
}
