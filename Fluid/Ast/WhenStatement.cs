using System.Text.Encodings.Web;
using Fluid.SourceGeneration;

namespace Fluid.Ast
{
    public sealed class WhenStatement : TagStatement, ISourceable
    {
        public WhenStatement(IReadOnlyList<Expression> options, IReadOnlyList<Statement> statements) : base(statements)
        {
            Options = options ?? [];
        }

        public IReadOnlyList<Expression> Options { get; }

        public override async ValueTask<Completion> WriteToAsync(TextWriter writer, TextEncoder encoder, TemplateContext context)
        {
            // Process statements until next block or end of statements
            for (var index = 0; index < Statements.Count; index++)
            {
                var completion = await Statements[index].WriteToAsync(writer, encoder, context);

                if (completion != Completion.Normal)
                {
                    // Stop processing the block statements
                    // We return the completion to flow it to the outer loop
                    return completion;
                }
            }

            return Completion.Normal;
        }

        protected internal override Statement Accept(AstVisitor visitor) => visitor.VisitWhenStatement(this);

        public void WriteTo(SourceGenerationContext context)
        {
            for (var i = 0; i < Statements.Count; i++)
            {
                var stmtMethod = context.GetStatementMethodName(Statements[i]);
                context.WriteLine($"var completion = await {stmtMethod}({context.WriterName}, {context.EncoderName}, {context.ContextName});");
                context.WriteLine("if (completion != Completion.Normal) return completion;");
            }

            context.WriteLine("return Completion.Normal;");
        }
    }
}
