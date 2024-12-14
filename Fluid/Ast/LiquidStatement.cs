using System.Text.Encodings.Web;

namespace Fluid.Ast
{
    public sealed class LiquidStatement : TagStatement
    {
        public LiquidStatement(IReadOnlyList<Statement> statements) : base(statements)
        {
        }

        protected internal override Statement Accept(AstVisitor visitor) => visitor.VisitLiquidStatement(this);

        public override async ValueTask<Completion> WriteToAsync(TextWriter writer, TextEncoder encoder, TemplateContext context)
        {
            context.IncrementSteps();

            for (var i = 0; i < Statements.Count; i++)
            {
                var statement = Statements[i];
                await statement.WriteToAsync(writer, encoder, context);
            }

            return Completion.Normal;
        }

        protected internal override Statement Accept(AstVisitor visitor) => visitor.VisitLiquidStatement(this);
    }
}
