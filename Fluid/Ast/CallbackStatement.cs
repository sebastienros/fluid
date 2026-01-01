using System.Text.Encodings.Web;

namespace Fluid.Ast
{
    /// <summary>
    /// An instance of this class is used to execute some custom code in a template.
    /// </summary>
    public sealed class CallbackStatement : Statement
    {
        public CallbackStatement(Func<IFluidOutput, TextEncoder, TemplateContext, ValueTask<Completion>> action)
        {
            Action = action;
        }

        public Func<IFluidOutput, TextEncoder, TemplateContext, ValueTask<Completion>> Action { get; }

        public override ValueTask<Completion> WriteToAsync(IFluidOutput output, TextEncoder encoder, TemplateContext context)
        {
            context.IncrementSteps();

            return Action?.Invoke(output, encoder, context) ?? new ValueTask<Completion>(Completion.Normal);
        }

        protected internal override Statement Accept(AstVisitor visitor) => visitor.VisitCallbackStatement(this);
    }
}
