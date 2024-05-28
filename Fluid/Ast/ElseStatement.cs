using System.Text.Encodings.Web;

namespace Fluid.Ast
{
    public sealed class ElseStatement : TagStatement
    {
        public ElseStatement(IReadOnlyList<Statement> statements) : base(statements)
        {
        }

        public override ValueTask<Completion> WriteToAsync(TextWriter writer, TextEncoder encoder, TemplateContext context)
        {
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

            for (var i = startIndex; i < Statements.Count; i++)
            {
                context.IncrementSteps();

                completion = await Statements[i].WriteToAsync(writer, encoder, context);

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
