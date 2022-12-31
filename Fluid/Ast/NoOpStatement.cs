using System.Text.Encodings.Web;

namespace Fluid.Ast
{
    public class NoOpStatement : Statement
    {
        protected internal override Statement Accept(AstVisitor visitor) => visitor.VisitNoOpStatement(this);

        public override ValueTask<Completion> WriteToAsync(TextWriter writer, TextEncoder encoder, TemplateContext context)
        {
            return Normal();
        }
    }
}
