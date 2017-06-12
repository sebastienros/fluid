using Fluid.Values;

namespace Fluid.Ast.BinaryExpressions
{
    public class DivideBinaryExpression : BinaryExpression
    {
        public DivideBinaryExpression(Expression left, Expression right) : base(left, right)
        {
        }

        public override FluidValue Evaluate(TemplateContext context)
        {
            var leftValue = Left.Evaluate(context);
            var rightValue = Right.Evaluate(context);

            if (leftValue is NumberValue && rightValue is NumberValue)
            {
                var rightNumber = rightValue.ToNumberValue();

                if(rightNumber == 0)
                {
                    return NilValue.Instance;
                }

                return new NumberValue(leftValue.ToNumberValue() / rightNumber);
            }

            return NilValue.Instance;
        }
    }
}
