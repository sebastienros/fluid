using Fluid.Ast;
using System.Text.Encodings.Web;

namespace Fluid.Parser
{
    public sealed class FluidTemplate : IFluidTemplate, IStatementList
    {
        public FluidTemplate(params Statement[] statements)
        {
            Statements = statements ?? [];
        }

        public FluidTemplate(IReadOnlyList<Statement> statements)
        {
            Statements = statements ?? throw new ArgumentNullException(nameof(statements));
        }

        public IReadOnlyList<Statement> Statements { get; }

        public ValueTask RenderAsync(TextWriter writer, TextEncoder encoder, TemplateContext context)
        {
            if (writer == null)
            {
                ExceptionHelper.ThrowArgumentNullException(nameof(writer));
            }

            if (encoder == null)
            {
                ExceptionHelper.ThrowArgumentNullException(nameof(encoder));
            }

            if (context == null)
            {
                ExceptionHelper.ThrowArgumentNullException(nameof(context));
            }

            // Clear missing variables from previous renders
            context.ClearMissingVariables();

            // If StrictVariables enabled, render to temp buffer to collect all missing variables
            TextWriter targetWriter = writer;
            if (context.Options.StrictVariables)
            {
                targetWriter = new StringWriter();
            }

            var count = Statements.Count;
            for (var i = 0; i < count; i++)
            {
                var task = Statements[i].WriteToAsync(targetWriter, encoder, context);
                if (!task.IsCompletedSuccessfully)
                {
                    return Awaited(
                        task,
                        writer,
                        targetWriter,
                        encoder,
                        context,
                        Statements,
                        startIndex: i + 1);
                }
            }

            // Check for missing variables after rendering
            if (context.Options.StrictVariables)
            {
                var missingVariables = context.GetMissingVariables();
                if (missingVariables.Count > 0)
                {
                    throw new StrictVariableException(missingVariables);
                }

                // Write buffered output to actual writer
                writer.Write(((StringWriter)targetWriter).ToString());
            }

            return new ValueTask();
        }

        private static async ValueTask Awaited(
            ValueTask<Completion> task,
            TextWriter writer,
            TextWriter targetWriter,
            TextEncoder encoder,
            TemplateContext context,
            IReadOnlyList<Statement> statements,
            int startIndex)
        {
            await task;
            for (var i = startIndex; i < statements.Count; i++)
            {
                await statements[i].WriteToAsync(targetWriter, encoder, context);
            }

            // Check for missing variables after async rendering
            if (context.Options.StrictVariables)
            {
                var missingVariables = context.GetMissingVariables();
                if (missingVariables.Count > 0)
                {
                    throw new StrictVariableException(missingVariables);
                }

                // Write buffered output to actual writer
                await writer.WriteAsync(((StringWriter)targetWriter).ToString());
            }
        }
    }
}
