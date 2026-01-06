using System.Text.Encodings.Web;
using Fluid.Values;
using Fluid.SourceGeneration;

namespace Fluid.Ast
{
    public sealed class IncrementStatement : Statement, ISourceable
    {
        public const string Prefix = "$$incdec$$$";
        public IncrementStatement(string identifier)
        {
            Identifier = identifier ?? "";
        }

        public string Identifier { get; }

        public override ValueTask<Completion> WriteToAsync(IFluidOutput output, TextEncoder encoder, TemplateContext context)
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

            var task = value.WriteToAsync(output, encoder, context.CultureInfo);
            return task.IsCompletedSuccessfully
                ? Statement.NormalCompletion
                : Awaited(task);

            static async ValueTask<Completion> Awaited(ValueTask t)
            {
                await t;
                return Completion.Normal;
            }
        }

        protected internal override Statement Accept(AstVisitor visitor) => visitor.VisitIncrementStatement(this);

        public void WriteTo(SourceGenerationContext context)
        {
            var identifierLit = SourceGenerationContext.ToCSharpStringLiteral(Identifier ?? "");

            context.WriteLine($"{context.ContextName}.IncrementSteps();");
            context.WriteLine($"var prefixedIdentifier = {SourceGenerationContext.ToCSharpStringLiteral(Prefix)} + {identifierLit};");
            context.WriteLine($"var value = {context.ContextName}.GetValue(prefixedIdentifier);");
            context.WriteLine("if (value.IsNil()) value = NumberValue.Zero; else value = NumberValue.Create(value.ToNumberValue() + 1);");
            context.WriteLine($"{context.ContextName}.SetValue(prefixedIdentifier, value);");
            context.WriteLine($"await value.WriteToAsync({context.WriterName}, {context.EncoderName}, {context.ContextName}.CultureInfo);");
            context.WriteLine("return Completion.Normal;");
        }
    }
}
