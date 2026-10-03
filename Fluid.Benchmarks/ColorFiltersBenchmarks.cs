using BenchmarkDotNet.Attributes;
using Fluid.Filters;
using Fluid.Values;

namespace Fluid.Benchmarks;

[MemoryDiagnoser]
public class ColorFiltersBenchmarks
{
    private readonly TemplateContext _context = new();
    private FluidValue _input;

    [Params("rgb(123, 182, 93)", "rgba(123, 182, 93, 0.5)",
        "hsl(100, 38%, 54%)", "hsla(100, 38%, 54%, 0.5)", "#7bd", "#7bb65d",
        "rgb(10.1, 11.2, 11.3, .4)", "rgb(10%, 10%, 20%, 40%)",
        "hsl(1rad, 50%, 50%)", "hsla(.25turn, 100%, 50%, 50%)")]
    public string Color { get; set; }

    [GlobalSetup]
    public void Setup() => _input = StringValue.Create(Color);

    [Benchmark]
    public FluidValue Brightness() => ColorFilters.CalculateBrightness(_input, FilterArguments.Empty, _context).Result;
}
