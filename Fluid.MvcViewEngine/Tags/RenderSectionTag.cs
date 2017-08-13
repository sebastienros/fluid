using System.Collections.Generic;
using System.IO;
using System.Text.Encodings.Web;
using System.Threading.Tasks;
using Fluid.Ast;
using Fluid.Tags;

namespace Fluid.MvcViewEngine.Tags
{
    public class RenderSectionTag : IdentifierTag
    {
        public override async Task<Completion> WriteToAsync(TextWriter writer, TextEncoder encoder, TemplateContext context, string sectionName)
        {
            if (context.AmbientValues.TryGetValue("Sections", out var sections))
            {
                var dictionary = sections as Dictionary<string, IList<Statement>>;
                if (dictionary.TryGetValue(sectionName, out var section))
                {
                    foreach(var statement in section)
                    {
                        await statement.WriteToAsync(writer, encoder, context);
                    }
                }
            }

            return Completion.Normal;
        }
    }
}
