using System.Text.Encodings.Web;
using Fluid.SourceGeneration;

namespace Fluid.Ast
{
    public class NoOpStatement : Statement, ISourceable
    {
        public override ValueTask<Completion> WriteToAsync(IFluidOutput output, TextEncoder encoder, TemplateContext context)
        {
            return Normal();
        }

        public void WriteTo(SourceGenerationContext context)
        {
            context.WriteLine("return Completion.Normal;");
        }

        protected internal override Statement Accept(AstVisitor visitor) => visitor.VisitNoOpStatement(this);
    }
}
