using System.Text.Encodings.Web;
using Fluid.Ast;

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

        public ValueTask RenderAsync(IFluidOutput output, TextEncoder encoder, TemplateContext context)
        {
            if (output == null)
            {
                ExceptionHelper.ThrowArgumentNullException(nameof(output));
            }

            if (encoder == null)
            {
                ExceptionHelper.ThrowArgumentNullException(nameof(encoder));
            }

            if (context == null)
            {
                ExceptionHelper.ThrowArgumentNullException(nameof(context));
            }

            var count = Statements.Count;
            for (var i = 0; i < count; i++)
            {
                var task = Statements[i].WriteToAsync(output, encoder, context);
                if (!task.IsCompletedSuccessfully)
                {
                    return Awaited(
                        task,
                        output,
                        encoder,
                        context,
                        Statements,
                        startIndex: i + 1);
                }
            }

            return output.FlushAsync();
        }

        private static async ValueTask Awaited(
            ValueTask<Completion> task,
            IFluidOutput output,
            TextEncoder encoder,
            TemplateContext context,
            IReadOnlyList<Statement> statements,
            int startIndex)
        {
            await task;
            for (var i = startIndex; i < statements.Count; i++)
            {
                await statements[i].WriteToAsync(output, encoder, context);
            }

            await output.FlushAsync();
        }
    }
}
