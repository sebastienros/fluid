using System.Text.Encodings.Web;

namespace Fluid.Ast
{
    public sealed class UnlessStatement : TagStatement
    {
        private readonly bool _isWhitespaceOrCommentOnly;

        public UnlessStatement(
            Expression condition,
            IReadOnlyList<Statement> statements,
            ElseStatement elseStatement = null,
            IReadOnlyList<ElseIfStatement> elseIfStatements = null) : base(statements)
        {
            Condition = condition;
            Else = elseStatement;
            ElseIfs = elseIfStatements ?? [];
            
            _isWhitespaceOrCommentOnly = true;

            for (var i = 0; i < Statements.Count; i++)
            {
                if (!Statements[i].IsWhitespaceOrCommentOnly)
                {
                    _isWhitespaceOrCommentOnly = false;
                    break;
                }
            }

            if (_isWhitespaceOrCommentOnly)
            {
                if (Else != null && !Else.IsWhitespaceOrCommentOnly)
                {
                    _isWhitespaceOrCommentOnly = false;
                }
                else
                {
                    for (var i = 0; i < ElseIfs.Count; i++)
                    {
                        if (!ElseIfs[i].IsWhitespaceOrCommentOnly)
                        {
                            _isWhitespaceOrCommentOnly = false;
                            break;
                        }
                    }
                }
            }
        }

        public override bool IsWhitespaceOrCommentOnly => _isWhitespaceOrCommentOnly;

        public Expression Condition { get; }
        public ElseStatement Else { get; }
        public IReadOnlyList<ElseIfStatement> ElseIfs { get; }

        public override async ValueTask<Completion> WriteToAsync(IFluidOutput output, TextEncoder encoder, TemplateContext context)
        {
            var result = (await Condition.EvaluateAsync(context)).ToBooleanValue();

            if (!result)
            {
                // Unless condition is false, execute the main block
                if (_isWhitespaceOrCommentOnly)
                {
                    // If the block is whitespace/comment/assign only, we execute statements but suppress output from TextSpanStatements
                    for (var i = 0; i < Statements.Count; i++)
                    {
                        var statement = Statements[i];
                        
                        // Skip writing TextSpanStatements (whitespace)
                        if (statement is TextSpanStatement)
                        {
                            continue;
                        }

                        var completion = await statement.WriteToAsync(output, encoder, context);

                        if (completion != Completion.Normal)
                        {
                            return completion;
                        }
                    }
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
                // Unless condition is true, check elsif branches (which use normal if logic)
                for (var i = 0; i < ElseIfs.Count; i++)
                {
                    var elseIf = ElseIfs[i];
                    var elseIfResult = (await elseIf.Condition.EvaluateAsync(context)).ToBooleanValue();

                    if (elseIfResult)
                    {
                        if (elseIf.IsWhitespaceOrCommentOnly)
                        {
                            // If the block is whitespace/comment/assign only, we execute statements but suppress output from TextSpanStatements
                            // ElseIfStatement.WriteToAsync handles this logic internally now
                        }

                        return await elseIf.WriteToAsync(output, encoder, context);
                    }
                }

                // No elsif matched, execute else if present
                if (Else != null)
                {
                    await Else.WriteToAsync(output, encoder, context);
                }
            }

            return Completion.Normal;
        }

        protected internal override Statement Accept(AstVisitor visitor) => visitor.VisitUnlessStatement(this);
    }
}
