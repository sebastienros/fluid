using System.IO;
using System.Text.Encodings.Web;
using System.Threading.Tasks;
using Fluid.Ast;
using Irony.Parsing;

namespace Fluid.Tags
{
    public abstract class IdentifierTag : ITag
    {
        public BnfTerm GetSyntax(FluidGrammar grammar)
        {
            return grammar.Identifier;
        }

        public Statement Parse(ParseTreeNode node, ParserContext context)
        {
            var identifier = node.ChildNodes[0].Token.ValueString;
            return new DelegateStatement((writer, encoder, ctx) => WriteToAsync(writer, encoder, ctx, identifier));
        }

        public abstract ValueTask<Completion> WriteToAsync(TextWriter writer, TextEncoder encoder, TemplateContext context, string identifier);
    }
}
