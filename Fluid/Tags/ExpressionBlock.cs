using System.Collections.Generic;
using System.IO;
using System.Text.Encodings.Web;
using System.Threading.Tasks;
using Fluid.Ast;
using Irony.Parsing;

namespace Fluid.Tags
{
    public abstract class ExpressionBlock : CustomBlock
    {
        public override BnfTerm GetSyntax(FluidGrammar grammar)
        {
            return grammar.Expression;
        }

        public override Statement Parse(ParseTreeNode node, ParserContext context)
        {
            var expression = DefaultFluidParser.BuildExpression(context.CurrentBlock.Tag.ChildNodes[0]);
            var statements = context.CurrentBlock.Statements;
            return new DelegateStatement((writer, encoder, ctx) => WriteToAsync(writer, encoder, ctx, expression, statements));
        }

        public abstract Task<Completion> WriteToAsync(TextWriter writer, TextEncoder encoder, TemplateContext context, Expression term, IList<Statement> statements);
    }
}
