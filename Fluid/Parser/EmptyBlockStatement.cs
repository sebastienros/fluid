using Fluid.Ast;
using System.Text.Encodings.Web;

namespace Fluid.Parser
{
    public sealed class EmptyBlockStatement : Statement
    {
        public EmptyBlockStatement(string tagName, IReadOnlyList<Statement> statements, Func<IReadOnlyList<Statement>, IFluidOutput, TextEncoder, TemplateContext, ValueTask<Completion>> render)
        {
            TagName = tagName ?? throw new ArgumentNullException(nameof(tagName));
            Render = render ?? throw new ArgumentNullException(nameof(render));
            Statements = statements ?? [];
        }

        public string TagName { get; }

        public IReadOnlyList<Statement> Statements { get; }

        public Func<IReadOnlyList<Statement>, IFluidOutput, TextEncoder, TemplateContext, ValueTask<Completion>> Render { get; }

        public override ValueTask<Completion> WriteToAsync(IFluidOutput output, TextEncoder encoder, TemplateContext context)
        {
            return Render(Statements, output, encoder, context);
        }

        protected internal override Statement Accept(AstVisitor visitor) => visitor.VisitEmptyBlockStatement(this);
    }
}
