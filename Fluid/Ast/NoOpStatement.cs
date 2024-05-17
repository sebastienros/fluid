using System.Text.Encodings.Web;

namespace Fluid.Ast
{
    public class NoOpStatement : Statement
    {
        public override ValueTask<Completion> WriteToAsync(TextWriter writer, TextEncoder encoder, TemplateContext context)
        {
            return Normal();
        }

        protected internal override Statement Accept(AstVisitor visitor) => visitor.VisitNoOpStatement(this);
    }
}
