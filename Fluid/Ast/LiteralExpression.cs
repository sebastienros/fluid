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

        protected internal override Expression Accept(AstVisitor visitor) => visitor.VisitLiteralExpression(this);

        public override ValueTask<FluidValue> EvaluateAsync(TemplateContext context)
        {
            return new ValueTask<FluidValue>(Value);
        }

        public override bool IsConstantExpression() => true;
    }
}
