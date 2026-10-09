using System;
using BenchmarkDotNet.Attributes;
using Fluid.Values;

namespace Fluid.Benchmarks;

/// <summary>
/// Writing numbers that can't use the precomputed text of small integers. Each shape takes a
/// different format: a fraction, a whole number that carries a scale (rendered with one decimal),
/// and an integer too large to be interned.
/// </summary>
[MemoryDiagnoser]
public class NumberOutputBenchmarks
{
    private const string Source = "{% for i in (1..100) %}{{ value }}{% endfor %}";

    private readonly IFluidTemplate _template;
    private readonly FluidValue _fraction = NumberValue.Create(19.99m);
    private readonly FluidValue _wholeWithScale = NumberValue.Create(20.00m);
    private readonly FluidValue _largeInteger = NumberValue.Create(123456m);

    public NumberOutputBenchmarks()
    {
        _template = new FluidParser().Parse(Source);

        Verify(nameof(Fraction), Fraction(), "19.9919.99");
        Verify(nameof(WholeWithScale), WholeWithScale(), "20.020.0");
        Verify(nameof(LargeInteger), LargeInteger(), "123456123456");

        static void Verify(string name, string result, string expected)
        {
            if (string.IsNullOrEmpty(result) || !result.StartsWith(expected, StringComparison.Ordinal))
            {
                throw new InvalidOperationException($"{name} rendering failed: {result}");
            }
        }
    }

    [Benchmark]
    public string Fraction()
    {
        return _template.Render(new TemplateContext().SetValue("value", _fraction));
    }

    [Benchmark]
    public string WholeWithScale()
    {
        return _template.Render(new TemplateContext().SetValue("value", _wholeWithScale));
    }

    [Benchmark]
    public string LargeInteger()
    {
        return _template.Render(new TemplateContext().SetValue("value", _largeInteger));
    }
}
