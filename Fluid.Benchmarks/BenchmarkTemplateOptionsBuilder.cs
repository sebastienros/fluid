namespace Fluid.Benchmarks;

/// <summary>
/// Builds options that read the products with source-generated accessors, like an application using the model
/// accessor generator.
/// </summary>
[FluidRegister(typeof(Product))]
public partial class BenchmarkTemplateOptionsBuilder : TemplateOptionsBuilder
{
}
