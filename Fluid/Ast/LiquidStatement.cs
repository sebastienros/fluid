using System.Text.Encodings.Web;

namespace Fluid.Ast
{
    public sealed class LiquidStatement : TagStatement
    {
        public LiquidStatement(List<Statement> statements) : base(statements)
        {
        }

        public override async ValueTask<Completion> WriteToAsync(TextWriter writer, TextEncoder encoder, TemplateContext context)
        {
            context.IncrementSteps();

            for (var i = 0; i < _statements.Count; i++)
            {
                var statement = _statements[i];
                await statement.WriteToAsync(writer, encoder, context);
            }

            return Completion.Normal;
        }

        protected internal override Statement Accept(AstVisitor visitor) => visitor.VisitLiquidStatement(this);
    }
}
