using Fluid.Ast;
using System.Text.Encodings.Web;

namespace Fluid.Parser
{
    internal sealed class ParserBlockStatement<T> : TagStatement
    {
        private readonly Func<T, IReadOnlyList<Statement>, TextWriter, TextEncoder, TemplateContext, ValueTask<Completion>> _render;

        public ParserBlockStatement(T value, IReadOnlyList<Statement> statements, Func<T, IReadOnlyList<Statement>, TextWriter, TextEncoder, TemplateContext, ValueTask<Completion>> render) : base(statements)
        {
            Value = value;
            _render = render ?? throw new ArgumentNullException(nameof(render));
        }

        public T Value { get; }

        protected internal override Statement Accept(AstVisitor visitor)
        {
            foreach (var statement in Statements)
            {
                statement.Accept(visitor);
            }

            return this;
        }

        public override ValueTask<Completion> WriteToAsync(TextWriter writer, TextEncoder encoder, TemplateContext context)
        {
            return _render(Value, Statements, writer, encoder, context);
        }
    }
}
