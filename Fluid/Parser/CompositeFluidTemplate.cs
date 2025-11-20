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

        public async ValueTask RenderAsync(TextWriter writer, TextEncoder encoder, TemplateContext context)
        {
            foreach (var template in Templates)
            {
                await template.RenderAsync(writer, encoder, context);
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
