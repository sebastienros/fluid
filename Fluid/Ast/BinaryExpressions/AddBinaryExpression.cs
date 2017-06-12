using Fluid.Values;

namespace Fluid.Ast.BinaryExpressions
{
    public class AddBinaryExpression : BinaryExpression
    {
        public AddBinaryExpression(Expression left, Expression right) : base(left, right)
        {
        }

        public override FluidValue Evaluate(TemplateContext context)
        {
            var leftValue = Left.Evaluate(context);
            var rightValue = Right.Evaluate(context);
            
            if (leftValue is StringValue)
            {
                return new StringValue(leftValue.ToStringValue() + rightValue.ToStringValue());
            }

            if (leftValue is NumberValue)
            {
                return new NumberValue(leftValue.ToNumberValue() + rightValue.ToNumberValue());
            }

            return NilValue.Instance;
        }
    }
}
