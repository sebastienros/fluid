using System.Text.Encodings.Web;

namespace Fluid.Ast
{
    public sealed class ContinueStatement : Statement
    {
        protected internal override Statement Accept(AstVisitor visitor) => visitor.VisitContinueStatement(this);

        public override ValueTask<Completion> WriteToAsync(TextWriter writer, TextEncoder encoder, TemplateContext context)
        {
            return Continue();
        }

        protected internal override Statement Accept(AstVisitor visitor) => visitor.VisitContinueStatement(this);
    }
}
