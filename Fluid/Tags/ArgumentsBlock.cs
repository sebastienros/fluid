using System.Collections.Generic;
using System.IO;
using System.Text.Encodings.Web;
using System.Threading.Tasks;
using Fluid.Ast;
using Irony.Parsing;

namespace Fluid.Tags
{
    public abstract class ArgumentsBlock : CustomBlock
    {
        public override BnfTerm GetSyntax(FluidGrammar grammar)
        {
            return grammar.FilterArguments;
        }

        public override Statement Parse(ParseTreeNode node, ParserContext context)
        {
            var e = context.CurrentBlock.Tag.ChildNodes[0];
            var arguments = new FilterArgument[e.ChildNodes.Count];
            for (var i = 0; i < e.ChildNodes.Count; i++)
            {
                arguments[i] = DefaultFluidParser.BuildFilterArgument(e.ChildNodes[i]);
            }

            var statements = context.CurrentBlock.Statements;
            return new DelegateStatement((writer, encoder, ctx) => WriteToAsync(writer, encoder, ctx, arguments, statements));
        }

        public abstract ValueTask<Completion> WriteToAsync(TextWriter writer, TextEncoder encoder, TemplateContext context, FilterArgument[] arguments, List<Statement> statements);
    }
}
