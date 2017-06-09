using Fluid.Ast.Values;

namespace Fluid.Ast.BinaryExpressions
{
    public class EqualBinaryExpression : BinaryExpression
    {
        public EqualBinaryExpression(Expression left, Expression right) : base(left, right)
        {
        }

        public override FluidValue Evaluate(TemplateContext context)
        {
            var leftValue = Left.Evaluate(context);
            var rightValue = Right.Evaluate(context);

            if (leftValue.Equals(rightValue))
            {
                return BooleanValue.True;
            }

            return BooleanValue.False;
        }
    }
}
