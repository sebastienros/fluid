using Fluid.Values;

namespace Fluid.Ast
{
    public sealed class LiteralExpression : Expression
    {
        public LiteralExpression(FluidValue value)
        {
            Value = value;
        }

        public FluidValue Value { get; }

        public override ValueTask<FluidValue> EvaluateAsync(TemplateContext context)
        {
            return Value;
        }

        protected internal override Expression Accept(AstVisitor visitor) => visitor.VisitLiteralExpression(this);
    }
}
