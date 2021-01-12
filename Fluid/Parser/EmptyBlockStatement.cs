using Fluid.Ast;
using System;
using System.Collections.Generic;
using System.IO;
using System.Text.Encodings.Web;
using System.Threading.Tasks;

namespace Fluid.Parser
{
    public class EmptyBlockStatement : Statement
    {
        private readonly Func<EmptyBlockStatement, TextWriter, TextEncoder, TemplateContext, ValueTask<Completion>> _render;

        public EmptyBlockStatement(List<Statement> statements, Func<EmptyBlockStatement, TextWriter, TextEncoder, TemplateContext, ValueTask<Completion>> render)
        {
            _render = render ?? throw new ArgumentNullException(nameof(render));
            Statements = statements ?? throw new ArgumentNullException(nameof(statements));
        }

        public List<Statement> Statements { get; }

        public async ValueTask<Completion> RenderBlockAsync(TextWriter writer, TextEncoder encoder, TemplateContext context)
        {
            for (var i = 0; i < Statements.Count; i++)
            {
                var statement = Statements[i];
                var completion = await statement.WriteToAsync(writer, encoder, context);

                if (completion != Completion.Normal)
                {
                    // Stop processing the block statements
                    // We return the completion to flow it to the outer loop
                    return completion;
                }
            }

            return Completion.Normal;
        }

        public override ValueTask<Completion> WriteToAsync(TextWriter writer, TextEncoder encoder, TemplateContext context)
        {
            return _render(this, writer, encoder, context);
        }
    }
}
