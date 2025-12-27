using Fluid.SourceGenerator;

namespace Fluid.Benchmarks
{
    // Generates one IFluidTemplate property per matching file in AdditionalFiles.
    // In this project we include *.liquid as AdditionalFiles.
    [FluidTemplates("*.liquid")]
    public static partial class SourceGeneratedTemplates
    {
    }
}
