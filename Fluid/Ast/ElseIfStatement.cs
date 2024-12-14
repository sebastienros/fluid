using System.Text.Encodings.Web;

namespace Fluid.Ast
{
    public sealed class ElseIfStatement : TagStatement
    {
        public ElseIfStatement(Expression condition, IReadOnlyList<Statement> statements) : base(statements)
        {
            Condition = condition;
        }

        public Expression Condition { get; }

        protected internal override Statement Accept(AstVisitor visitor) => visitor.VisitElseIfStatement(this);

        public override ValueTask<Completion> WriteToAsync(TextWriter writer, TextEncoder encoder, TemplateContext context)
        {
            // Process statements until next block or end of statements
            for (var i = 0; i < Statements.Count; i++)
            {
                context.IncrementSteps();

                var task = Statements[i].WriteToAsync(writer, encoder, context);
                if (!task.IsCompletedSuccessfully)
                {
                    return Awaited(task, i + 1, writer, encoder, context);
                }

                var completion = task.Result;
                if (completion != Completion.Normal)
                {
                    // Stop processing the block statements
                    // We return the completion to flow it to the outer loop
                    return new ValueTask<Completion>(completion);
                }
            }

            return new ValueTask<Completion>(Completion.Normal);
        }

        private async ValueTask<Completion> Awaited(
            ValueTask<Completion> task,
            int startIndex,
            TextWriter writer,
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
                completion = await Statements[index].WriteToAsync(writer, encoder, context);
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
