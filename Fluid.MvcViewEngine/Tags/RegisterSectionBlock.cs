using System.Collections.Generic;
using System.IO;
using System.Text.Encodings.Web;
using System.Threading.Tasks;
using Fluid.Ast;
using Fluid.Tags;

namespace Fluid.MvcViewEngine.Tags
{
    public class RegisterSectionBlock : IdentifierBlock
    {
        public override Task<Completion> WriteToAsync(TextWriter writer, TextEncoder encoder, TemplateContext context, string sectionName, List<Statement> statements)
        {
            if (context.AmbientValues.TryGetValue("Sections", out var sections))
            {
                var dictionary = sections as Dictionary<string, List<Statement>>;
                dictionary[sectionName] = statements;
            }

            return Task.FromResult(Completion.Normal);
        }
    }
}
