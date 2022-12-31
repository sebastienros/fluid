using Fluid.Ast;
using System.Text.Encodings.Web;

namespace Fluid.Parser
{
    internal sealed class ParserTagStatement<T> : Statement
    {
        private readonly Func<T, TextWriter, TextEncoder, TemplateContext, ValueTask<Completion>> _render;

        public ParserTagStatement(T value, Func<T, TextWriter, TextEncoder, TemplateContext, ValueTask<Completion>> render)
        {
            Value = value;
            _render = render ?? throw new ArgumentNullException(nameof(render));
        }

        public T Value { get; }

        protected internal override Statement Accept(AstVisitor visitor) => this;

        public override ValueTask<Completion> WriteToAsync(TextWriter writer, TextEncoder encoder, TemplateContext context)
        {
            return _render(Value, writer, encoder, context);
        }
    }
}
