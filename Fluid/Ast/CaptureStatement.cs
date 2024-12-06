using Fluid.Utils;
using Fluid.Values;
using System.Text.Encodings.Web;

namespace Fluid.Ast
{
    public sealed class CaptureStatement : TagStatement
    {
        public CaptureStatement(string identifier, IReadOnlyList<Statement> statements) : base(statements)
        {
            Identifier = identifier;
        }

        public string Identifier { get; }

        public override async ValueTask<Completion> WriteToAsync(TextWriter writer, TextEncoder encoder, TemplateContext context)
        {
            var completion = Completion.Normal;

            using var sb = StringBuilderPool.GetInstance();
            using var sw = new StringWriter(sb.Builder);
            for (var i = 0; i < Statements.Count; i++)
            {
                completion = await Statements[i].WriteToAsync(sw, encoder, context);

                if (completion != Completion.Normal)
                {
                    // Stop processing the block statements
                    // We return the completion to flow it to the outer loop
                    break;
                }
            }

            var result = sw.ToString();

            // Substitute the result if a custom callback is provided
            if (context.Captured != null)
            {
                result = await context.Captured.Invoke(Identifier, result);
            }

            context.Assigned?.Invoke(new StringValue(result));

            // Don't encode captured blocks
            context.SetValue(Identifier, new StringValue(result, false));

            return completion;
        }

        protected internal override Statement Accept(AstVisitor visitor) => visitor.VisitCaptureStatement(this);
    }
}
