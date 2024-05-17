using Fluid.Ast;
using System.Text.Encodings.Web;

namespace Fluid.Parser
{
    internal sealed class EmptyTagStatement : Statement
    {
        private readonly Func<TextWriter, TextEncoder, TemplateContext, ValueTask<Completion>> _render;

        public EmptyTagStatement(Func<TextWriter, TextEncoder, TemplateContext, ValueTask<Completion>> render)
        {
            _render = render ?? throw new ArgumentNullException(nameof(render));
        }

        public override ValueTask<Completion> WriteToAsync(TextWriter writer, TextEncoder encoder, TemplateContext context)
        {
            return _render(writer, encoder, context);
        }
    }
}
