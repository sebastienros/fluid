using Fluid.Ast;
using System;
using System.Collections.Generic;
using System.IO;
using System.Text.Encodings.Web;
using System.Threading.Tasks;

namespace Fluid.Parlot
{
    public class ParserBlockStatement<T> : TagStatement
    {
        private readonly Func<ParserBlockStatement<T>, TextWriter, TextEncoder, TemplateContext, ValueTask<Completion>> _render;

        public ParserBlockStatement(T value, IReadOnlyList<Statement> statements, Func<ParserBlockStatement<T>, TextWriter, TextEncoder, TemplateContext, ValueTask<Completion>> render) : base(statements)
        {
            Value = value;
            _render = render ?? throw new ArgumentNullException(nameof(render));
        }

        public T Value { get; }

        public async ValueTask<Completion> RenderBlockAsync(TextWriter writer, TextEncoder encoder, TemplateContext context)
        {
            for (var i = 0; i < _statements.Count; i++)
            {
                var statement = _statements[i];
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
