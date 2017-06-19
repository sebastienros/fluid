using System.Collections.Generic;
using System.IO;
using System.Text.Encodings.Web;
using System.Threading.Tasks;
using Fluid.Ast;

namespace Fluid.MvcViewEngine.Statements
{
    public class RenderSectionStatement : Statement
    {
        public RenderSectionStatement(string sectionName)
        {
            SectionName = sectionName;
        }

        public string SectionName { get; }

        public override async Task<Completion> WriteToAsync(TextWriter writer, TextEncoder encoder, TemplateContext context)
        {
            if (context.AmbientValues.TryGetValue("Sections", out var sections))
            {
                var dictionary = sections as Dictionary<string, IList<Statement>>;
                if (dictionary.TryGetValue(SectionName, out var section))
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
