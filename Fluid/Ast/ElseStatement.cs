using System.Text.Encodings.Web;

namespace Fluid.Ast
{
    public sealed class ElseStatement : TagStatement
    {
        private readonly bool _isWhitespaceOrCommentOnly;

        public ElseStatement(IReadOnlyList<Statement> statements) : base(statements)
        {
            _isWhitespaceOrCommentOnly = StatementListHelper.IsWhitespaceOrCommentOnly(Statements);
        }

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

            for (var i = startIndex; i < Statements.Count; i++)
            {
                context.IncrementSteps();

                completion = await Statements[i].WriteToAsync(output, encoder, context);

                if (completion != Completion.Normal)
                {
                    // Stop processing the block statements
                    // We return the completion to flow it to the outer loop
                    return completion;
                }
            }

            return Completion.Normal;
        }

        protected internal override Statement Accept(AstVisitor visitor) => visitor.VisitElseStatement(this);
    }
}
