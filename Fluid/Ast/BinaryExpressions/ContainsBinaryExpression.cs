using Fluid.Ast.Values;

namespace Fluid.Ast.BinaryExpressions
{
    public class ContainsBinaryExpression : BinaryExpression
    {
        public ContainsBinaryExpression(Expression left, Expression right) : base(left, right)
        {
        }

        public override FluidValue Evaluate(TemplateContext context)
        {
            var leftValue = Left.Evaluate(context);
            var rightValue = Right.Evaluate(context);

            if (leftValue is StringValue && rightValue is StringValue)
            {
                if (leftValue.ToStringValue().Contains(rightValue.ToStringValue()))
                {
                    return BooleanValue.True;
                }
            }

            return BooleanValue.False;
        }
    }
}
