using Fluid.Values;

namespace Fluid.Ast.BinaryExpressions
{
    public class AndBinaryExpression : BinaryExpression
    {
        public AndBinaryExpression(Expression left, Expression right) : base(left, right)
        {
        }

        public override FluidValue Evaluate(TemplateContext context)
        {
            var leftValue = Left.Evaluate(context);
            var rightValue = Right.Evaluate(context);

            return new BooleanValue(leftValue.ToBooleanValue() && rightValue.ToBooleanValue());
        }
    }
}
