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

        protected internal override Statement Accept(AstVisitor visitor) => visitor.VisitCaptureStatement(this);

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

            FluidValue result = new StringValue(sw.ToString(), false);

            // Substitute the result if a custom callback is provided
            if (context.Captured != null)
            {
                result = await context.Captured.Invoke(Identifier, result, context);
            }

            // Don't encode captured blocks
            context.SetValue(Identifier, result);

            return completion;
        }

        protected internal override Statement Accept(AstVisitor visitor) => visitor.VisitCaptureStatement(this);
    }
}
