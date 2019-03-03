using System.IO;
using System.Text.Encodings.Web;
using System.Threading.Tasks;
using Fluid.Ast;
using Irony.Parsing;

namespace Fluid.Tags
{
    public abstract class ExpressionTag : ITag
    {
        public BnfTerm GetSyntax(FluidGrammar grammar)
        {
            return grammar.Expression;
        }

        public Statement Parse(ParseTreeNode node, ParserContext context)
        {
            var expression = DefaultFluidParser.BuildExpression(node.ChildNodes[0]);
            return new DelegateStatement((writer, encoder, ctx) => WriteToAsync(writer, encoder, ctx, expression));
        }

        public abstract ValueTask<Completion> WriteToAsync(TextWriter writer, TextEncoder encoder, TemplateContext context, Expression term);
    }
}
