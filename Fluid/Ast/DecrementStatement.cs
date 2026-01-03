using Fluid.Values;
using System.Text.Encodings.Web;

namespace Fluid.Ast
{
    public sealed class DecrementStatement : Statement
    {
        public DecrementStatement(string identifier)
        {
            Identifier = identifier ?? "";
        }

        public string Identifier { get; }

        public override bool IsWhitespaceOrCommentOnly => true;

        public override async ValueTask<Completion> WriteToAsync(IFluidOutput output, TextEncoder encoder, TemplateContext context)
        {
            context.IncrementSteps();

            // We prefix the identifier to prevent collisions with variables.
            // Variable identifiers don't represent the same slots as inc/dec ones.
            // c.f. https://shopify.github.io/liquid/tags/variable/

            var prefixedIdentifier = IncrementStatement.Prefix + Identifier;

            var value = context.GetValue(prefixedIdentifier);

            if (value.IsNil())
            {
                value = NumberValue.Create(-1);
            }
            else
            {
                value = NumberValue.Create(value.ToNumberValue() - 1);
            }

            context.SetValue(prefixedIdentifier, value);

            await value.WriteToAsync(output, encoder, context.CultureInfo);

            return Completion.Normal;
        }

        protected internal override Statement Accept(AstVisitor visitor) => visitor.VisitDecrementStatement(this);
    }
}
