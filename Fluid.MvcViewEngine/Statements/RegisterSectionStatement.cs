using System.Collections.Generic;
using System.IO;
using System.Text.Encodings.Web;
using System.Threading.Tasks;
using Fluid.Ast;

namespace Fluid.MvcViewEngine.Statements
{
    public class RegisterSectionStatement : TagStatement
    {
        public RegisterSectionStatement(string sectionName, IList<Statement> statements): base(statements)
        {
            SectionName = sectionName;
        }

        public string SectionName { get; } 

        public override Task<Completion> WriteToAsync(TextWriter writer, TextEncoder encoder, TemplateContext context)
        {
            if (context.AmbientValues.TryGetValue("Sections", out var sections))
            {
                var dictionary = sections as Dictionary<string, IList<Statement>>;
                dictionary[SectionName] = Statements;
            }

            return Task.FromResult(Completion.Normal);
        }
    }
}
