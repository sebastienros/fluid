using System.Text.Encodings.Web;

namespace Fluid.Ast
{
    public sealed class WhenStatement : TagStatement
    {
        public WhenStatement(IReadOnlyList<Expression> options, List<Statement> statements) : base(statements)
        {
            Options = options ?? [];
        }

        public IReadOnlyList<Expression> Options { get; }

        public override async ValueTask<Completion> WriteToAsync(TextWriter writer, TextEncoder encoder, TemplateContext context)
        {
            // Process statements until next block or end of statements
            for (var index = 0; index < _statements.Count; index++)
            {
                var completion = await _statements[index].WriteToAsync(writer, encoder, context);

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
    }
}
