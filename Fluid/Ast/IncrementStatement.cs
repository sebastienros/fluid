using System.Text.Encodings.Web;
using Fluid.Values;

namespace Fluid.Ast
{
    public sealed class IncrementStatement : Statement
    {
        public const string Prefix = "$$incdec$$$";
        public IncrementStatement(string identifier)
        {
            Identifier = identifier ?? "";
        }

        public string Identifier { get; }

        public override bool IsWhitespaceOrCommentOnly => true;

        public override ValueTask<Completion> WriteToAsync(IFluidOutput output, TextEncoder encoder, TemplateContext context)
        {
            context.IncrementSteps();

            // We prefix the identifier to prevent collisions with variables.
            // Variable identifiers don't represent the same slots as inc/dec ones.
            // c.f. https://shopify.github.io/liquid/tags/variable/

            var prefixedIdentifier = Prefix + Identifier;

            var value = context.GetValue(prefixedIdentifier);

            decimal current;
            if (value.IsNil())
            {
                current = 0;
                value = NumberValue.Zero;
            }
            else
            {
                current = value.ToNumberValue();
            }

            var nextValue = NumberValue.Create(current + 1);

            // Increment renders the value before incrementing it.
            var task = value.WriteToAsync(output, encoder, context.CultureInfo);
            if (task.IsCompletedSuccessfully)
            {
                context.SetValue(prefixedIdentifier, nextValue);
                return Statement.NormalCompletion;
            }

            return Awaited(task, context, prefixedIdentifier, nextValue);

            static async ValueTask<Completion> Awaited(ValueTask t, TemplateContext ctx, string key, FluidValue next)
            {
                await t;
                ctx.SetValue(key, next);
                return Completion.Normal;
            }
        }

        protected internal override Statement Accept(AstVisitor visitor) => visitor.VisitIncrementStatement(this);
    }
}
