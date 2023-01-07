using System.Text.Encodings.Web;

namespace Fluid.Ast
{
    /// <summary>
    /// An instance of this class is used to execute some custom code in a template.
    /// </summary>
    public class CallbackStatement : Statement
    {
        public CallbackStatement(Func<TextWriter, TextEncoder, TemplateContext, ValueTask<Completion>> action)
        {
            Action = action;
        }

        public Func<TextWriter, TextEncoder, TemplateContext, ValueTask<Completion>> Action { get; }

        protected internal override Statement Accept(AstVisitor visitor) => visitor.VisitCallbackStatement(this);

        public override ValueTask<Completion> WriteToAsync(TextWriter writer, TextEncoder encoder, TemplateContext context)
        {
            context.IncrementSteps();

            return Action?.Invoke(writer, encoder, context) ?? new ValueTask<Completion>(Completion.Normal);
        }
    }
}
