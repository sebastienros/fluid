using Fluid.Values;

namespace Fluid.Ast.BinaryExpressions
{
    public sealed class MultiplyBinaryExpression : BinaryExpression
    {
        public MultiplyBinaryExpression(Expression left, Expression right) : base(left, right)
        {
        }

        internal override FluidValue Evaluate(FluidValue leftValue, FluidValue rightValue, TemplateContext context)
        {
            return leftValue is NumberValue && rightValue is NumberValue
                ? NumberValue.Create(leftValue.ToNumberValue(context) * rightValue.ToNumberValue(context))
                : NilValue.Instance;
        }

        protected internal override Expression Accept(AstVisitor visitor) => visitor.VisitMultiplyBinaryExpression(this);
    }
}
