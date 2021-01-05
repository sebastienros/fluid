using Fluid.Ast;
using System;
using System.IO;
using System.Text.Encodings.Web;
using System.Threading.Tasks;

namespace Fluid.Parlot
{
    public class EmptyTagStatement : Statement
    {
        private readonly Func<EmptyTagStatement, TextWriter, TextEncoder, TemplateContext, ValueTask<Completion>> _render;

        public EmptyTagStatement(Func<EmptyTagStatement, TextWriter, TextEncoder, TemplateContext, ValueTask<Completion>> render)
        {
            _render = render ?? throw new ArgumentNullException(nameof(render));
        }

        public override ValueTask<Completion> WriteToAsync(TextWriter writer, TextEncoder encoder, TemplateContext context)
        {
            return _render(this, writer, encoder, context);
        }
    }
}
