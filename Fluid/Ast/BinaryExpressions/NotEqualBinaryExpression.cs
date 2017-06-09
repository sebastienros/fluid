using Fluid.Ast.Values;

namespace Fluid.Ast.BinaryExpressions
{
    public class NotEqualBinaryExpression : BinaryExpression
    {
        public NotEqualBinaryExpression(Expression left, Expression right) : base(left, right)
        {
        }

        public override FluidValue Evaluate(TemplateContext context)
        {
            var leftValue = Left.Evaluate(context);
            var rightValue = Right.Evaluate(context);

            if (leftValue.Equals(rightValue))
            {
                return BooleanValue.False;
            }

            return BooleanValue.True;
        }
    }
}
