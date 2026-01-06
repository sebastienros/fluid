using Fluid.SourceGeneration;

namespace Fluid
{
    public static partial class FluidTemplateExtensions
    {
        public static TemplateSource Compile(this IFluidTemplate template, SourceGenerationOptions options = null)
        {
            return TemplateSourceGenerator.Generate(template, options);
        }
    }
}
