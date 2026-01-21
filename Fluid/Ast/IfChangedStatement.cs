using Fluid.Utils;
using System.Text.Encodings.Web;

namespace Fluid.Ast
{
    public sealed class IfChangedStatement : TagStatement
    {
        private const string IfChangedRegisterKey = "$$ifchanged$$";

        public IfChangedStatement(IReadOnlyList<Statement> statements) : base(statements)
        {
        }

        public override async ValueTask<Completion> WriteToAsync(TextWriter writer, TextEncoder encoder, TemplateContext context)
        {
            context.IncrementSteps();

            // Get the previous value (shared across all ifchanged blocks)
            context.AmbientValues.TryGetValue(IfChangedRegisterKey, out var previousValueObj);
            var previousValue = previousValueObj as string;

            // Render inner statements to a buffer
            using var builder = StringBuilderPool.GetInstance();
            using var captureWriter = new StringWriter(builder.Builder);
            var completion = Completion.Normal;

            for (var i = 0; i < Statements.Count; i++)
            {
                completion = await Statements[i].WriteToAsync(captureWriter, encoder, context);

                if (completion != Completion.Normal)
                {
                    break;
                }
            }

            var currentValue = captureWriter.ToString();

            // Output only if content has changed from the last ifchanged output
            if (!string.Equals(previousValue, currentValue, StringComparison.Ordinal))
            {
                context.AmbientValues[IfChangedRegisterKey] = currentValue;
                writer.Write(currentValue);
            }

            return completion;
        }

        protected internal override Statement Accept(AstVisitor visitor) => visitor.VisitIfChangedStatement(this);
    }
}
