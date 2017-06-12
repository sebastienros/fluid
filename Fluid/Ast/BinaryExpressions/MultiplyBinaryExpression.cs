using Fluid.Values;

namespace Fluid.Ast.BinaryExpressions
{
    public class MultiplyBinaryExpression : BinaryExpression
    {
        public MultiplyBinaryExpression(Expression left, Expression right) : base(left, right)
        {
        }

        public override FluidValue Evaluate(TemplateContext context)
        {
            var leftValue = Left.Evaluate(context);
            var rightValue = Right.Evaluate(context);

            if (leftValue is NumberValue && rightValue is NumberValue)
            {
                return new NumberValue(leftValue.ToNumberValue() * rightValue.ToNumberValue());
            }

            return NilValue.Instance;
        }
    }
}
