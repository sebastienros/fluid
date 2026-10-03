using System.Text.Encodings.Web;
using Fluid.Ast;
using Fluid.Utils;

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
            ArgumentNullException.ThrowIfNull(output);
            ArgumentNullException.ThrowIfNull(encoder);
            ArgumentNullException.ThrowIfNull(context);

            context.CancellationToken.ThrowIfCancellationRequested();
            output = LimitedFluidOutput.Create(output, context.MaxOutputSize);

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

            context.CancellationToken.ThrowIfCancellationRequested();
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

            context.CancellationToken.ThrowIfCancellationRequested();
            await output.FlushAsync();
        }
    }
}
