using System.Collections.Generic;
using System.IO;
using System.Text.Encodings.Web;
using System.Threading.Tasks;
using Fluid.Ast;
using Irony.Parsing;

namespace Fluid.Tags
{
    public abstract class IdentifierBlock : CustomBlock
    {
        public override BnfTerm GetSyntax(FluidGrammar grammar)
        {
            return grammar.Identifier;
        }

        public override Statement Parse(ParseTreeNode node, ParserContext context)
        {
            var identifier = context.CurrentBlock.Tag.ChildNodes[0].Token.ValueString;
            var statements = context.CurrentBlock.Statements;
            return new DelegateStatement((writer, encoder, ctx) => WriteToAsync(writer, encoder, ctx, identifier, statements));
        }

        public abstract Task<Completion> WriteToAsync(TextWriter writer, TextEncoder encoder, TemplateContext context, string identifier, List<Statement> statements);
    }
}
