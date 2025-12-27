using System.Text.Encodings.Web;
using Fluid.SourceGeneration;

namespace Fluid.Ast
{
    public sealed class LiquidStatement : TagStatement, ISourceable
    {
        public LiquidStatement(IReadOnlyList<Statement> statements) : base(statements)
        {
        }

        public override async ValueTask<Completion> WriteToAsync(TextWriter writer, TextEncoder encoder, TemplateContext context)
        {
            context.IncrementSteps();

            for (var i = 0; i < Statements.Count; i++)
            {
                var statement = Statements[i];
                var completion = await statement.WriteToAsync(writer, encoder, context);

                if (completion != Completion.Normal)
                {
                    // Stop processing the block statements
                    // We return the completion to flow it to the outer loop
                    return completion;
                }
            }

            return Completion.Normal;
        }

        protected internal override Statement Accept(AstVisitor visitor) => visitor.VisitLiquidStatement(this);

        public void WriteTo(SourceGenerationContext context)
        {
            context.WriteLine($"{context.ContextName}.IncrementSteps();");

            for (var i = 0; i < Statements.Count; i++)
            {
                var stmtMethod = context.GetStatementMethodName(Statements[i]);
                context.WriteLine($"var completion{i} = await {stmtMethod}({context.WriterName}, {context.EncoderName}, {context.ContextName});");
                context.WriteLine($"if (completion{i} != Completion.Normal) return completion{i};");
            }

            context.WriteLine("return Completion.Normal;");
        }
    }
}
