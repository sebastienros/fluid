using System.Linq;
using System.Text.Encodings.Web;
using BenchmarkDotNet.Attributes;
using Fluid.Values;

namespace Fluid.Benchmarks;

[MemoryDiagnoser]
public class ArrayOutputBenchmarks
{
    private readonly IFluidTemplate _template = new FluidParser().Parse("{{ items }}");
    private readonly FluidValue _strings = FluidValue.Create(
        Enumerable.Repeat("<b>x</b>", 100).ToArray(), TemplateOptions.Default);
    private readonly FluidValue _numbers = FluidValue.Create(
        Enumerable.Repeat(2.50m, 100).ToArray(), TemplateOptions.Default);

    [Benchmark]
    public string Strings() => _template.Render(
        new TemplateContext().SetValue("items", _strings), HtmlEncoder.Default);

    [Benchmark]
    public string Numbers() => _template.Render(
        new TemplateContext().SetValue("items", _numbers), HtmlEncoder.Default);
}
