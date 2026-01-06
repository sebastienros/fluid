using Fluid.Values;
using System.Text.Encodings.Web;
using Fluid.SourceGeneration;

namespace Fluid.Ast
{
    public sealed class DecrementStatement : Statement, ISourceable
    {
        public DecrementStatement(string identifier)
        {
            Identifier = identifier;
        }

        public string Identifier { get; }

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
                value = NumberValue.Zero;
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

        public void WriteTo(SourceGenerationContext context)
        {
            var identifierLit = SourceGenerationContext.ToCSharpStringLiteral(Identifier ?? "");

            context.WriteLine($"{context.ContextName}.IncrementSteps();");
            context.WriteLine($"var prefixedIdentifier = {SourceGenerationContext.ToCSharpStringLiteral(IncrementStatement.Prefix)} + {identifierLit};");
            context.WriteLine($"var value = {context.ContextName}.GetValue(prefixedIdentifier);");
            context.WriteLine("if (value.IsNil()) value = NumberValue.Zero; else value = NumberValue.Create(value.ToNumberValue() - 1);");
            context.WriteLine($"{context.ContextName}.SetValue(prefixedIdentifier, value);");
            context.WriteLine($"await value.WriteToAsync({context.WriterName}, {context.EncoderName}, {context.ContextName}.CultureInfo);");
            context.WriteLine("return Completion.Normal;");
        }
    }
}
