using System.Text.Encodings.Web;

namespace Fluid.Ast
{
    public abstract class Statement
    {
        public static readonly ValueTask<Completion> BreakCompletion = new(Completion.Break);
        public static readonly ValueTask<Completion> NormalCompletion = new(Completion.Normal);
        public static readonly ValueTask<Completion> ContinueCompletion = new(Completion.Continue);

        public static ValueTask<Completion> FromCompletion(Completion completion) => completion switch
        {
            Completion.Normal => NormalCompletion,
            Completion.Break => BreakCompletion,
            Completion.Continue => ContinueCompletion,
            _ => new(completion)
        };

        public static ValueTask<Completion> Break() => BreakCompletion;
        public static ValueTask<Completion> Normal() => NormalCompletion;
        public static ValueTask<Completion> Continue() => ContinueCompletion;

        public abstract ValueTask<Completion> WriteToAsync(IFluidOutput output, TextEncoder encoder, TemplateContext context);

        protected internal virtual Statement Accept(AstVisitor visitor) => visitor.VisitOtherStatement(this);
    }
}