using System.IO;
using System.Text.Encodings.Web;
using System.Threading.Tasks;
using Fluid.Values;

namespace Fluid.Ast
{
    public class IncrementStatement : Statement
    {
        public const string Prefix = "$$incdec$$$";
        public IncrementStatement(string identifier)
        {
            Identifier = identifier ?? "";
        }

        public string Identifier { get; }

        public override ValueTask<Completion> WriteToAsync(TextWriter writer, TextEncoder encoder, TemplateContext context)
        {
            context.IncrementSteps();

            // We prefix the identifier to prevent collisions with variables.
            // Variable identifiers don't represent the same slots as inc/dec ones.
            // c.f. https://shopify.github.io/liquid/tags/variable/

            var prefixedIdentifier = Prefix + Identifier;

            var value = context.GetValue(prefixedIdentifier);

            if (value.IsNil())
            {
                value = NumberValue.Zero;
            }
            else
            {
                value = NumberValue.Create(value.ToNumberValue() + 1);
            }

            context.SetValue(prefixedIdentifier, value);

            value.WriteTo(writer, encoder, context.CultureInfo);

            return Normal();
        }
    }
}
