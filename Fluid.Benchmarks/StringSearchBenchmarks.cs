using System;
using System.Linq;
using BenchmarkDotNet.Attributes;

namespace Fluid.Benchmarks;

/// <summary>
/// Filters and operators that look for one string inside another. The text is long and the match
/// sits at the far end of the search, so the cost of the search itself dominates.
/// </summary>
[MemoryDiagnoser]
public class StringSearchBenchmarks
{
    private const string Needle = "NEEDLE";

    private readonly string _needleLast;
    private readonly string _needleFirst;
    private readonly IFluidTemplate _removeFirst;
    private readonly IFluidTemplate _removeLast;
    private readonly IFluidTemplate _replaceFirst;
    private readonly IFluidTemplate _replaceLast;
    private readonly IFluidTemplate _startsWith;
    private readonly IFluidTemplate _endsWith;

    public StringSearchBenchmarks()
    {
        var filler = string.Concat(Enumerable.Repeat("lorem ipsum dolor sit amet ", 400));
        _needleLast = filler + Needle;
        _needleFirst = Needle + filler;

        var parser = new FluidParser();
        _removeFirst = parser.Parse("{{ text | remove_first: 'NEEDLE' | size }}");
        _removeLast = parser.Parse("{{ text | remove_last: 'NEEDLE' | size }}");
        _replaceFirst = parser.Parse("{{ text | replace_first: 'NEEDLE', 'x' | size }}");
        _replaceLast = parser.Parse("{{ text | replace_last: 'NEEDLE', 'x' | size }}");
        _startsWith = parser.Parse("{% for i in (1..100) %}{% if text startswith 'NEEDLE' %}y{% endif %}{% endfor %}");
        _endsWith = parser.Parse("{% for i in (1..100) %}{% if text endswith 'NEEDLE' %}y{% endif %}{% endfor %}");

        var removed = filler.Length.ToString();
        var replaced = (filler.Length + 1).ToString();

        Verify(nameof(RemoveFirst), RemoveFirst(), removed);
        Verify(nameof(RemoveLast), RemoveLast(), removed);
        Verify(nameof(ReplaceFirst), ReplaceFirst(), replaced);
        Verify(nameof(ReplaceLast), ReplaceLast(), replaced);
        Verify(nameof(StartsWith), StartsWith(), new string('y', 100));
        Verify(nameof(EndsWith), EndsWith(), new string('y', 100));

        static void Verify(string name, string result, string expected)
        {
            if (result != expected)
            {
                throw new InvalidOperationException($"{name} rendering failed: {result}");
            }
        }
    }

    [Benchmark]
    public string RemoveFirst()
    {
        return _removeFirst.Render(new TemplateContext().SetValue("text", _needleLast));
    }

    [Benchmark]
    public string RemoveLast()
    {
        return _removeLast.Render(new TemplateContext().SetValue("text", _needleFirst));
    }

    [Benchmark]
    public string ReplaceFirst()
    {
        return _replaceFirst.Render(new TemplateContext().SetValue("text", _needleLast));
    }

    [Benchmark]
    public string ReplaceLast()
    {
        return _replaceLast.Render(new TemplateContext().SetValue("text", _needleFirst));
    }

    [Benchmark]
    public string StartsWith()
    {
        return _startsWith.Render(new TemplateContext().SetValue("text", _needleFirst));
    }

    [Benchmark]
    public string EndsWith()
    {
        return _endsWith.Render(new TemplateContext().SetValue("text", _needleLast));
    }
}
