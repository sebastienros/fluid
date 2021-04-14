using System.Collections.Generic;
using System.IO;
using System.Text.Encodings.Web;
using System.Threading.Tasks;

namespace Fluid.Ast
{
    public class ElseIfStatement : TagStatement
    {
        public ElseIfStatement(Expression condition, List<Statement> statements) : base(statements)
        {
            Condition = condition;
        }

        public Expression Condition { get; }

        public override ValueTask<Completion> WriteToAsync(TextWriter writer, TextEncoder encoder, TemplateContext context)
        {
            // Process statements until next block or end of statements
            for (var i = 0; i < _statements.Count; i++)
            {
                context.IncrementSteps();

                var task = _statements[i].WriteToAsync(writer, encoder, context);
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
            for (var index = startIndex; index < _statements.Count; index++)
            {
                context.IncrementSteps();
                completion = await _statements[index].WriteToAsync(writer, encoder, context);
                if (completion != Completion.Normal)
                {
                    // Stop processing the block statements
                    // We return the completion to flow it to the outer loop
                    return completion;
                }
            }

            return Completion.Normal;
        }
    }
}
