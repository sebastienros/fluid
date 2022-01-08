using System.Collections.Generic;
using System.IO;
using System.Text.Encodings.Web;
using System.Threading.Tasks;

namespace Fluid.Parser
{
    public sealed class CompositeFluidTemplate : IFluidTemplate
    {
        private readonly List<IFluidTemplate> _templates;

        public CompositeFluidTemplate(IEnumerable<IFluidTemplate> templates)
        {
            _templates = new List<IFluidTemplate>(templates);
        }

        public async ValueTask RenderAsync(TextWriter writer, TextEncoder encoder, TemplateContext context)
        {
            for (var i = 0; i < _templates.Count; i++)
            {
                var template = _templates[i];
                await template.RenderAsync(writer, encoder, context);
            }
        }
    }
}
