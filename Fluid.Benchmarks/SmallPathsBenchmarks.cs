using System;
using System.Collections.Generic;
using System.Linq;
using BenchmarkDotNet.Attributes;
using Fluid.Values;

namespace Fluid.Benchmarks;

/// <summary>
/// Small operations that are cheap once but add up inside a loop: counters, a dictionary keyed by
/// integers, the modulo filter, turning an array into a string, the contains operator and an indexer.
/// Each one runs 100 times per render.
/// </summary>
[MemoryDiagnoser]
public class SmallPathsBenchmarks
{
    private readonly FluidValue _byId;
    private readonly FluidValue _items;
    private readonly IFluidTemplate _counters;
    private readonly IFluidTemplate _integerKeyedDictionary;
    private readonly IFluidTemplate _modulo;
    private readonly IFluidTemplate _arrayToString;
    private readonly IFluidTemplate _contains;
    private readonly IFluidTemplate _indexer;

    public SmallPathsBenchmarks()
    {
        _byId = FluidValue.Create(Enumerable.Range(0, 1000).ToDictionary(i => i, i => "v" + i), TemplateOptions.Default);
        _items = FluidValue.Create(new[] { "a", "b", "c", "d", "e", "f", "g", "h", "i", "j" }, TemplateOptions.Default);

        var parser = new FluidParser();
        _counters = parser.Parse("{% for i in (1..100) %}{% increment up %}{% decrement down %}{% endfor %}");
        _integerKeyedDictionary = parser.Parse("{% for i in (1..100) %}{{ byId[500] }}{% endfor %}");
        _modulo = parser.Parse("{% for i in (1..100) %}{{ i | modulo: 7 }}{% endfor %}");
        _arrayToString = parser.Parse("{% for i in (1..100) %}{% assign s = items | append: '' %}{% endfor %}{{ s }}");
        _contains = parser.Parse("{% for i in (1..100) %}{% if text contains 'needle' %}y{% endif %}{% endfor %}");
        _indexer = parser.Parse("{% for i in (1..100) %}{{ items[2] }}{% endfor %}");

        Verify(nameof(Counters), Counters(), "0-11-2");
        Verify(nameof(IntegerKeyedDictionary), IntegerKeyedDictionary(), "v500v500");
        Verify(nameof(Modulo), Modulo(), "1234560");
        Verify(nameof(ArrayToString), ArrayToString(), "abcdefghij");
        Verify(nameof(Contains), Contains(), "yy");
        Verify(nameof(Indexer), Indexer(), "cc");

        static void Verify(string name, string result, string expected)
        {
            if (string.IsNullOrEmpty(result) || !result.StartsWith(expected, StringComparison.Ordinal))
            {
                throw new InvalidOperationException($"{name} rendering failed: {result}");
            }
        }
    }

    [Benchmark]
    public string Counters()
    {
        return _counters.Render(new TemplateContext());
    }

    [Benchmark]
    public string IntegerKeyedDictionary()
    {
        return _integerKeyedDictionary.Render(new TemplateContext().SetValue("byId", _byId));
    }

    [Benchmark]
    public string Modulo()
    {
        return _modulo.Render(new TemplateContext());
    }

    [Benchmark]
    public string ArrayToString()
    {
        return _arrayToString.Render(new TemplateContext().SetValue("items", _items));
    }

    [Benchmark]
    public string Contains()
    {
        return _contains.Render(new TemplateContext().SetValue("text", "a haystack with a needle in it"));
    }

    [Benchmark]
    public string Indexer()
    {
        return _indexer.Render(new TemplateContext().SetValue("items", _items));
    }
}
