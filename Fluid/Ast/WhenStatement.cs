using System.Text.Encodings.Web;

namespace Fluid.Ast
{
    public sealed class WhenStatement : TagStatement
    {
        public WhenStatement(IReadOnlyList<Expression> options, IReadOnlyList<Statement> statements) : base(statements)
        {
            Options = options ?? [];
        }

        public IReadOnlyList<Expression> Options { get; }

        public override async ValueTask<Completion> WriteToAsync(IFluidOutput output, TextEncoder encoder, TemplateContext context)
        {
            // Process statements until next block or end of statements
            for (var index = 0; index < Statements.Count; index++)
            {
            var completion = await Statements[index].WriteToAsync(output, encoder, context);

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
