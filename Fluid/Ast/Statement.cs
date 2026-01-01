using System.Text.Encodings.Web;

namespace Fluid.Ast
{
    public abstract class Statement
    {
        public static ValueTask<Completion> Break() => new(Completion.Break);
        public static ValueTask<Completion> Normal() => new(Completion.Normal);
        public static ValueTask<Completion> Continue() => new(Completion.Continue);

        public abstract ValueTask<Completion> WriteToAsync(IFluidOutput output, TextEncoder encoder, TemplateContext context);

        protected internal virtual Statement Accept(AstVisitor visitor) => visitor.VisitOtherStatement(this);
    }
}