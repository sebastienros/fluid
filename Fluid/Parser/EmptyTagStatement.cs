using Fluid.Ast;
using System.Text.Encodings.Web;

namespace Fluid.Parser
{
    public sealed class EmptyTagStatement : Statement
    {
        private readonly Func<TextWriter, TextEncoder, TemplateContext, ValueTask<Completion>> _render;

        public string TagName { get; }

        public EmptyTagStatement(string tagName, Func<TextWriter, TextEncoder, TemplateContext, ValueTask<Completion>> render)
        {
            TagName = tagName ?? throw new ArgumentNullException(nameof(tagName));
            _render = render ?? throw new ArgumentNullException(nameof(render));
        }

        public override ValueTask<Completion> WriteToAsync(TextWriter writer, TextEncoder encoder, TemplateContext context)
        {
            return _render(writer, encoder, context);
        }

        protected internal override Statement Accept(AstVisitor visitor) => visitor.VisitEmptyTagStatement(this);
    }
}
