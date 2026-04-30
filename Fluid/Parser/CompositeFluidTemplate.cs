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

        public ValueTask RenderAsync(IFluidOutput output, TextEncoder encoder, TemplateContext context)
        {
            ArgumentNullException.ThrowIfNull(output);
            ArgumentNullException.ThrowIfNull(encoder);
            ArgumentNullException.ThrowIfNull(context);

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
