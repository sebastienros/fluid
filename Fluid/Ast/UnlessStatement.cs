using System.Text.Encodings.Web;

namespace Fluid.Ast
{
    public sealed class UnlessStatement : TagStatement
    {
        private readonly bool _isWhitespaceOrCommentOnly;

        public UnlessStatement(
            Expression condition,
            IReadOnlyList<Statement> statements,
            ElseStatement elseStatement = null) : base(statements)
        {
            Condition = condition;
            Else = elseStatement;
            _isWhitespaceOrCommentOnly = StatementListHelper.IsWhitespaceOrCommentOnly(Statements);
        }

        public Expression Condition { get; }
        public ElseStatement Else { get; }

        public override async ValueTask<Completion> WriteToAsync(IFluidOutput output, TextEncoder encoder, TemplateContext context)
        {
            var result = (await Condition.EvaluateAsync(context)).ToBooleanValue();

            if (!result)
            {
                if (_isWhitespaceOrCommentOnly)
                {
                    return Completion.Normal;
                }

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
            else
            {
                if (Else != null)
                {
                    if (!Else.IsWhitespaceOrCommentOnly)
                    {
                    await Else.WriteToAsync(output, encoder, context);
                    }
                }
            }

            return Completion.Normal;
        }

        protected internal override Statement Accept(AstVisitor visitor) => visitor.VisitUnlessStatement(this);
    }
}
