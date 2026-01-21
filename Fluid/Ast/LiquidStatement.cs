using System.Text.Encodings.Web;

namespace Fluid.Ast
{
    public sealed class LiquidStatement : TagStatement
    {
        public LiquidStatement(IReadOnlyList<Statement> statements) : base(statements)
        {
        }

        public override bool IsWhitespaceOrCommentOnly
        {
            get
            {
                for (var i = 0; i < Statements.Count; i++)
                {
                    if (!Statements[i].IsWhitespaceOrCommentOnly)
                    {
                        return false;
                    }
                }
                return true;
            }
        }

        public override async ValueTask<Completion> WriteToAsync(IFluidOutput output, TextEncoder encoder, TemplateContext context)
        {
            context.IncrementSteps();

            for (var i = 0; i < Statements.Count; i++)
            {
                var statement = Statements[i];
                var completion = await statement.WriteToAsync(output, encoder, context);

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
    }
}
