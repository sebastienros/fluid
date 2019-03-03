using System.IO;
using System.Text.Encodings.Web;
using System.Threading.Tasks;
using Fluid.Ast;
using Irony.Parsing;

namespace Fluid.Tags
{
    public abstract class SimpleTag : ITag
    {
        public BnfTerm GetSyntax(FluidGrammar grammar)
        {
            return grammar.Empty;
        }

        public Statement Parse(ParseTreeNode node, ParserContext context)
        {
            return new DelegateStatement(WriteToAsync);
        }

        public abstract ValueTask<Completion> WriteToAsync(TextWriter writer, TextEncoder encoder, TemplateContext context);
    }
}
