using System.Collections.Generic;
using System.IO;
using System.Text.Encodings.Web;
using System.Threading.Tasks;
using Fluid.Ast;
using Irony.Parsing;

namespace Fluid.Tags
{
    public abstract class CustomBlock : ITag
    {
        public abstract BnfTerm GetSyntax(FluidGrammar grammar);

        public abstract Statement Parse(ParseTreeNode node, ParserContext context);

        public async Task<Completion> RenderStatementsAsync(TextWriter writer, TextEncoder encoder, TemplateContext context, List<Statement> statements)
        {
            Completion completion;

            foreach (var statement in statements)
            {
                completion = await statement.WriteToAsync(writer, encoder, context);

                if (completion != Completion.Normal)
                {
                    // Stop processing the block statements
                    return completion;
                }
            }

            return Completion.Normal;
        }
    }
}
