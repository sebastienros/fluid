using Fluid.Ast;
using System.Text.Encodings.Web;

namespace Fluid.Parser
{
    public sealed class ParserBlockStatement<T> : TagStatement
    {
        public ParserBlockStatement(string tagName, T value, IReadOnlyList<Statement> statements, Func<T, IReadOnlyList<Statement>, IFluidOutput, TextEncoder, TemplateContext, ValueTask<Completion>> render) : base(statements)
        {
            Value = value;
            TagName = tagName ?? throw new ArgumentNullException(nameof(tagName));
            Render = render ?? throw new ArgumentNullException(nameof(render));
        }
        public Func<T, IReadOnlyList<Statement>, IFluidOutput, TextEncoder, TemplateContext, ValueTask<Completion>> Render { get; }

        public string TagName { get; }

        public T Value { get; }

        public override ValueTask<Completion> WriteToAsync(IFluidOutput output, TextEncoder encoder, TemplateContext context)
        {
            return Render(Value, Statements, output, encoder, context);
        }

        protected internal override Statement Accept(AstVisitor visitor) => visitor.VisitParserBlockStatement(this);
    }
}
