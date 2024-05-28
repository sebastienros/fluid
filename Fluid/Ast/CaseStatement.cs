using System.Text.Encodings.Web;

namespace Fluid.Ast
{
    public sealed class CaseStatement : TagStatement
    {
        private readonly WhenStatement[] _whenStatements;

        public CaseStatement(
            Expression expression,
            ElseStatement elseStatement = null,
            WhenStatement[] whenStatements = null
        ) : base([])
        {
            Expression = expression;
            Else = elseStatement;
            _whenStatements = whenStatements ?? [];
        }

        public Expression Expression { get; }

        public ElseStatement Else { get; }

        public IReadOnlyList<WhenStatement> Whens => _whenStatements;

        public override async ValueTask<Completion> WriteToAsync(TextWriter writer, TextEncoder encoder, TemplateContext context)
        {
            context.IncrementSteps();

            var value = await Expression.EvaluateAsync(context);

            var elseShouldBeEvaluated = true;

            foreach (var when in _whenStatements)
            {
                foreach (var option in when.Options)
                {
                    if (value.Equals(await option.EvaluateAsync(context)))
                    {
                        elseShouldBeEvaluated = false;
                        await when.WriteToAsync(writer, encoder, context);
                    }
                }
            }

            if (elseShouldBeEvaluated && Else != null)
            {
                await Else.WriteToAsync(writer, encoder, context);
            }

            return Completion.Normal;
        }

        protected internal override Statement Accept(AstVisitor visitor) => visitor.VisitCaseStatement(this);
    }
}