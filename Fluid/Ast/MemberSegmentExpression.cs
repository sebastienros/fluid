using Fluid.Ast.Values;

namespace Fluid.Ast
{
    public abstract class MemberSegmentExpression : Expression
    {
    }

    public class IdentifierSegmentIdentiferExpression : MemberSegmentExpression
    {
        public IdentifierSegmentIdentiferExpression(string identifier)
        {
            Identifier = identifier;
        }

        public string Identifier { get; }

        public override FluidValue Evaluate(TemplateContext context)
        {
            throw new System.NotImplementedException();
        }
    }

    public class IndexerSegmentIdentiferExpression : MemberSegmentExpression
    {
        public IndexerSegmentIdentiferExpression(Expression expression)
        {
            Expression = expression;
        }

        public Expression Expression { get; }

        public override FluidValue Evaluate(TemplateContext context)
        {
            throw new System.NotImplementedException();
        }
    }
}
