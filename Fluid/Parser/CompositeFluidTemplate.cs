using System.Collections.Generic;
using System.IO;
using System.Text.Encodings.Web;
using System.Threading.Tasks;

namespace Fluid.Parser
{
    public class CompositeFluidTemplate : IFluidTemplate
    {
        private readonly List<IFluidTemplate> _templates;

        public CompositeFluidTemplate(params IFluidTemplate[] templates)
        {
            _templates = new List<IFluidTemplate>(templates);
        }

        public CompositeFluidTemplate(IEnumerable<IFluidTemplate> templates)
        {
            _templates = new List<IFluidTemplate>(templates);
        }

        public IReadOnlyList<IFluidTemplate> Templates => _templates;

        public async ValueTask RenderAsync(TextWriter writer, TextEncoder encoder, TemplateContext context)
        {
            foreach (var template in Templates)
            {
                await template.RenderAsync(writer, encoder, context);
            }
        }
    }
}
