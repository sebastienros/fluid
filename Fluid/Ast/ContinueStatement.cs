using System.Text.Encodings.Web;

namespace Fluid.Ast
{
    public sealed class ContinueStatement : Statement
    {
        public override ValueTask<Completion> WriteToAsync(IFluidOutput output, TextEncoder encoder, TemplateContext context)
        {
            return Continue();
        }

        protected internal override Statement Accept(AstVisitor visitor) => visitor.VisitContinueStatement(this);
    }
}
