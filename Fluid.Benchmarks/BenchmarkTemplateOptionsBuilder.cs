namespace Fluid.Benchmarks;

/// <summary>
/// Builds options with source-generated accessors for the benchmark models. The view model is detected from
/// <c>new TemplateContext(model, options)</c>, the products it contains have to be registered.
/// </summary>
[FluidRegister(typeof(Product))]
public partial class BenchmarkTemplateOptionsBuilder : TemplateOptionsBuilder
{
}
