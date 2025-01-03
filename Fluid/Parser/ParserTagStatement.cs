using Fluid.Ast;
using System.Text.Encodings.Web;

namespace Fluid.Parser
{
    public sealed class ParserTagStatement<T> : Statement
    {
        public ParserTagStatement(string tagName, T value, Func<T, TextWriter, TextEncoder, TemplateContext, ValueTask<Completion>> render)
        {
            Value = value;
            TagName = tagName ?? throw new ArgumentNullException(nameof(tagName));
            Render = render ?? throw new ArgumentNullException(nameof(render));
        }

        public Func<T, TextWriter, TextEncoder, TemplateContext, ValueTask<Completion>> Render { get; }

        public string TagName { get; }

        public T Value { get; }

        public override ValueTask<Completion> WriteToAsync(TextWriter writer, TextEncoder encoder, TemplateContext context)
        {
            return Render(Value, writer, encoder, context);
        }

        protected internal override Statement Accept(AstVisitor visitor) => visitor.VisitParserTagStatement(this);
    }
}
