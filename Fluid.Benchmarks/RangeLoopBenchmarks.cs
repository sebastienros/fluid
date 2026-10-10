using System;
using BenchmarkDotNet.Attributes;
using Fluid.Values;

namespace Fluid.Benchmarks;

/// <summary>
/// A small inner loop executed many times, so that what a <c>for</c> does once per execution, rather
/// than once per item, is what gets measured. The range sources are compared with the same loop
/// over an array holding the same three numbers.
/// </summary>
[MemoryDiagnoser]
public class RangeLoopBenchmarks
{
    private const int Executions = 1000;

    private readonly FluidValue _array = FluidValue.Create(new[] { 1, 2, 3 }, TemplateOptions.Default);
    private readonly IFluidTemplate _literalRange;
    private readonly IFluidTemplate _variableRange;
    private readonly IFluidTemplate _arraySource;

    public RangeLoopBenchmarks()
    {
        var parser = new FluidParser();
        _literalRange = parser.Parse($"{{% for o in (1..{Executions}) %}}{{% for i in (1..3) %}}{{{{ i }}}}{{% endfor %}}{{% endfor %}}");
        _variableRange = parser.Parse($"{{% for o in (1..{Executions}) %}}{{% for i in (first..last) %}}{{{{ i }}}}{{% endfor %}}{{% endfor %}}");
        _arraySource = parser.Parse($"{{% for o in (1..{Executions}) %}}{{% for i in items %}}{{{{ i }}}}{{% endfor %}}{{% endfor %}}");

        Verify(nameof(LiteralRange), LiteralRange());
        Verify(nameof(VariableRange), VariableRange());
        Verify(nameof(ArraySource), ArraySource());

        static void Verify(string name, string result)
        {
            if (result is null || result.Length != Executions * 3 || !result.StartsWith("123123", StringComparison.Ordinal))
            {
                throw new InvalidOperationException($"{name} rendering failed: {result}");
            }
        }
    }

    [Benchmark]
    public string LiteralRange()
    {
        return _literalRange.Render(new TemplateContext());
    }

    [Benchmark]
    public string VariableRange()
    {
        return _variableRange.Render(new TemplateContext().SetValue("first", 1).SetValue("last", 3));
    }

    [Benchmark]
    public string ArraySource()
    {
        return _arraySource.Render(new TemplateContext().SetValue("items", _array));
    }
}
