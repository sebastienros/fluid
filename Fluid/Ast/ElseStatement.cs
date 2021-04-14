using System.Collections.Generic;
using System.IO;
using System.Text.Encodings.Web;
using System.Threading.Tasks;

namespace Fluid.Ast
{
    public class ElseStatement : TagStatement
    {
        public ElseStatement(List<Statement> statements) : base(statements)
        {
        }

        public override ValueTask<Completion> WriteToAsync(TextWriter writer, TextEncoder encoder, TemplateContext context)
        {
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

            for (var i = startIndex; i < _statements.Count; i++)
            {
                context.IncrementSteps();

                completion = await _statements[i].WriteToAsync(writer, encoder, context);

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
