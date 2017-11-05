using System.Collections.Generic;
using System.IO;
using System.Linq;
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
            var arguments = e.ChildNodes.Select(DefaultFluidParser.BuildFilterArgument).ToArray();
            var statements = context.CurrentBlock.Statements;
            return new DelegateStatement((writer, encoder, ctx) => WriteToAsync(writer, encoder, ctx, arguments, statements));
        }

        public abstract Task<Completion> WriteToAsync(TextWriter writer, TextEncoder encoder, TemplateContext context, FilterArgument[] arguments, IList<Statement> statements);
    }
}
