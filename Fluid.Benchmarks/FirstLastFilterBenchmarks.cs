using System;
using System.Linq;
using BenchmarkDotNet.Attributes;
using Fluid.Values;

namespace Fluid.Benchmarks;

/// <summary>
/// The <c>first</c> and <c>last</c> filters next to the equivalent member access, on an array large
/// enough to show whether the filter's cost depends on the number of items.
/// </summary>
[MemoryDiagnoser]
public class FirstLastFilterBenchmarks
{
    private const int ItemCount = 10_000;

    private readonly FluidValue _items;
    private readonly IFluidTemplate _firstFilter;
    private readonly IFluidTemplate _lastFilter;
    private readonly IFluidTemplate _firstMember;
    private readonly IFluidTemplate _lastMember;

    public FirstLastFilterBenchmarks()
    {
        _items = FluidValue.Create(Enumerable.Range(1, ItemCount).ToArray(), TemplateOptions.Default);

        var parser = new FluidParser();
        _firstFilter = parser.Parse("{{ items | first }}");
        _lastFilter = parser.Parse("{{ items | last }}");
        _firstMember = parser.Parse("{{ items.first }}");
        _lastMember = parser.Parse("{{ items.last }}");

        Verify(nameof(FirstFilter), FirstFilter(), "1");
        Verify(nameof(LastFilter), LastFilter(), "10000");
        Verify(nameof(FirstMember), FirstMember(), "1");
        Verify(nameof(LastMember), LastMember(), "10000");

        static void Verify(string name, string result, string expected)
        {
            if (result != expected)
            {
                throw new InvalidOperationException($"{name} rendering failed: {result}");
            }
        }
    }

    [Benchmark]
    public string FirstFilter()
    {
        return _firstFilter.Render(new TemplateContext().SetValue("items", _items));
    }

    [Benchmark]
    public string LastFilter()
    {
        return _lastFilter.Render(new TemplateContext().SetValue("items", _items));
    }

    [Benchmark]
    public string FirstMember()
    {
        return _firstMember.Render(new TemplateContext().SetValue("items", _items));
    }

    [Benchmark]
    public string LastMember()
    {
        return _lastMember.Render(new TemplateContext().SetValue("items", _items));
    }
}
