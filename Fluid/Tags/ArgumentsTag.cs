using System.IO;
using System.Linq;
using System.Text.Encodings.Web;
using System.Threading.Tasks;
using Fluid.Ast;
using Irony.Parsing;

namespace Fluid.Tags
{
    public abstract class ArgumentsTag : ITag
    {
        public BnfTerm GetSyntax(FluidGrammar grammar)
        {
            return grammar.FilterArguments;
        }

        public Statement Parse(ParseTreeNode node, ParserContext context)
        {
            var arguments = node.ChildNodes[0].ChildNodes.Select(DefaultFluidParser.BuildFilterArgument).ToArray();
            return new DelegateStatement((writer, encoder, ctx) => WriteToAsync(writer, encoder, ctx, arguments));
        }

        public abstract Task<Completion> WriteToAsync(TextWriter writer, TextEncoder encoder, TemplateContext context, FilterArgument[] arguments);
    }
}
