using Fluid.Ast;
using System.Text.Encodings.Web;

namespace Fluid.Parser
{
    public sealed class CompositeFluidTemplate : IFluidTemplate, IStatementList
    {
        public CompositeFluidTemplate(params IFluidTemplate[] templates)
        {
            Templates = new List<IFluidTemplate>(templates);
            Statements = CollectStatements(templates);
        }

        public CompositeFluidTemplate(IReadOnlyList<IFluidTemplate> templates)
        {
            Templates = new List<IFluidTemplate>(templates);
            Statements = CollectStatements(templates);
        }

        public IReadOnlyList<IFluidTemplate> Templates { get; }

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

            var count = Statements.Count;
            for (var i = 0; i < count; i++)
            {
                var task = Statements[i].WriteToAsync(writer, encoder, context);
                if (!task.IsCompletedSuccessfully)
                {
                    return Awaited(
                        task,
                        writer,
                        encoder,
                        context,
                        Statements,
                        startIndex: i + 1);
                }
            }

            return new ValueTask();
        }

        private static async ValueTask Awaited(
            ValueTask<Completion> task,
            TextWriter writer,
            TextEncoder encoder,
            TemplateContext context,
            IReadOnlyList<Statement> statements,
            int startIndex)
        {
            await task;
            for (var i = startIndex; i < statements.Count; i++)
            {
                await statements[i].WriteToAsync(writer, encoder, context);
            }
        }

        private static List<Statement> CollectStatements(IEnumerable<IFluidTemplate> templates)
        {
            var statements = new List<Statement>();
            
            foreach (var template in templates)
            {
                if (template is IStatementList statementList)
                {
                    statements.AddRange(statementList.Statements);
                }
            }
            
            return statements;
        }
    }
}
