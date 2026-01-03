using System.Text.Encodings.Web;

namespace Fluid.Ast
{
    public sealed class ElseIfStatement : TagStatement
    {
        private readonly bool _isWhitespaceOrCommentOnly;

        public ElseIfStatement(Expression condition, IReadOnlyList<Statement> statements) : base(statements)
        {
            Condition = condition;
            _isWhitespaceOrCommentOnly = StatementListHelper.IsWhitespaceOrCommentOnly(Statements);
        }

        public Expression Condition { get; }

        internal bool IsWhitespaceOrCommentOnly => _isWhitespaceOrCommentOnly;

        public override ValueTask<Completion> WriteToAsync(IFluidOutput output, TextEncoder encoder, TemplateContext context)
        {
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

                    context.IncrementSteps();

                    var task = statement.WriteToAsync(output, encoder, context);
                    if (!task.IsCompletedSuccessfully)
                    {
                        return Awaited(task, i + 1, output, encoder, context);
                    }

                    var completion = task.Result;
                    if (completion != Completion.Normal)
                    {
                        return Statement.FromCompletion(completion);
                    }
                }
                return Statement.NormalCompletion;
            }

            // Process statements until next block or end of statements
            for (var i = 0; i < Statements.Count; i++)
            {
                context.IncrementSteps();

                var task = Statements[i].WriteToAsync(output, encoder, context);
                if (!task.IsCompletedSuccessfully)
                {
                    return Awaited(task, i + 1, output, encoder, context);
                }

                var completion = task.Result;
                if (completion != Completion.Normal)
                {
                    // Stop processing the block statements
                    // We return the completion to flow it to the outer loop
                    return Statement.FromCompletion(completion);
                }
            }

            return Statement.NormalCompletion;
        }

        private async ValueTask<Completion> Awaited(
            ValueTask<Completion> task,
            int startIndex,
            IFluidOutput output,
            TextEncoder encoder,
            TemplateContext context)
        {
            var completion = await task;
            if (completion != Completion.Normal)
            {
                // Stop processing the block statements
                // We return the completion to flow it to the outer loop
                return completion;
            }
            // Process statements until next block or end of statements
            for (var index = startIndex; index < Statements.Count; index++)
            {
                context.IncrementSteps();
                completion = await Statements[index].WriteToAsync(output, encoder, context);
                if (completion != Completion.Normal)
                {
                    // Stop processing the block statements
                    // We return the completion to flow it to the outer loop
                    return completion;
                }
            }

            return Completion.Normal;
        }

        protected internal override Statement Accept(AstVisitor visitor) => visitor.VisitElseIfStatement(this);
    }
}
