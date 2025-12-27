using System.Text.Encodings.Web;
using Fluid.SourceGeneration;

namespace Fluid.Ast
{
    public sealed class BreakStatement : Statement, ISourceable
    {
        public override ValueTask<Completion> WriteToAsync(TextWriter writer, TextEncoder encoder, TemplateContext context)
        {
            return Break();
        }

        public void WriteTo(SourceGenerationContext context)
        {
            context.WriteLine("return Completion.Break;");
        }

        protected internal override Statement Accept(AstVisitor visitor) => visitor.VisitBreakStatement(this);
    }
}
