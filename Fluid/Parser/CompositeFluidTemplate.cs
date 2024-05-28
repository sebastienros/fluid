using System.Text.Encodings.Web;

namespace Fluid.Parser
{
    public sealed class CompositeFluidTemplate : IFluidTemplate
    {
        public CompositeFluidTemplate(params IFluidTemplate[] templates)
        {
            Templates = new List<IFluidTemplate>(templates);
        }

        public CompositeFluidTemplate(IReadOnlyList<IFluidTemplate> templates)
        {
            Templates = new List<IFluidTemplate>(templates);
        }

        public IReadOnlyList<IFluidTemplate> Templates { get; }

        public async ValueTask RenderAsync(TextWriter writer, TextEncoder encoder, TemplateContext context)
        {
            foreach (var template in Templates)
            {
                await template.RenderAsync(writer, encoder, context);
            }
        }
    }
}
