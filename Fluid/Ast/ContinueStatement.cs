using System.Text.Encodings.Web;
using Fluid.SourceGeneration;

namespace Fluid.Ast
{
    public sealed class ContinueStatement : Statement, ISourceable
    {
        public override ValueTask<Completion> WriteToAsync(TextWriter writer, TextEncoder encoder, TemplateContext context)
        {
            return Continue();
        }

        public void WriteTo(SourceGenerationContext context)
        {
            context.WriteLine("return Completion.Continue;");
        }

        protected internal override Statement Accept(AstVisitor visitor) => visitor.VisitContinueStatement(this);
    }
}
