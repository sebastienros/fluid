using System.Text.Encodings.Web;

namespace Fluid.Compilation;

/// <summary>
/// Wraps the function returned by a template compilation result into an <see cref="IFluidTemplate"/> implementation.
/// </summary>
internal class CompiledTemplate : IFluidTemplate
{
    private readonly IFluidTemplate _fluidTemplate;

    public CompiledTemplate(IFluidTemplate fluidTemplate)
    {
        _fluidTemplate = fluidTemplate;
    }

    public ValueTask RenderAsync(TextWriter writer, TextEncoder encoder, TemplateContext context)
    {
        return _fluidTemplate.RenderAsync(writer, encoder, context);
    }
}
