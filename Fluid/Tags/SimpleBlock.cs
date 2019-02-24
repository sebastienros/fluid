using System.Collections.Generic;
using System.IO;
using System.Text.Encodings.Web;
using System.Threading.Tasks;
using Fluid.Ast;
using Irony.Parsing;

namespace Fluid.Tags
{
    public abstract class SimpleBlock : CustomBlock
    {
        public override BnfTerm GetSyntax(FluidGrammar grammar)
        {
            return grammar.Empty;
        }

        public override Statement Parse(ParseTreeNode node, ParserContext context)
        {
            var statements = context.CurrentBlock.Statements;
            return new DelegateStatement((writer, encoder, ctx) => WriteToAsync(writer, encoder, ctx, statements));
        }

        public abstract ValueTask<Completion> WriteToAsync(TextWriter writer, TextEncoder encoder, TemplateContext context, List<Statement> statements);
    }
}
