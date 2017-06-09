using Fluid.Ast.Values;

namespace Fluid.Ast.BinaryExpressions
{
    public class GreaterThanBinaryExpression : BinaryExpression
    {
        public GreaterThanBinaryExpression(Expression left, Expression right, bool strict) : base(left, right)
        {
            Strict = strict;
        }

        public bool Strict { get; }

        public override FluidValue Evaluate(TemplateContext context)
        {
            var leftValue = Left.Evaluate(context);
            var rightValue = Right.Evaluate(context);
            
            if (leftValue is NumberValue)
            {
                if (Strict)
                {
                    return leftValue.ToNumberValue() > rightValue.ToNumberValue()
                        ? BooleanValue.True
                        : BooleanValue.False
                        ;
                }
                else
                {
                    return leftValue.ToNumberValue() >= rightValue.ToNumberValue()
                        ? BooleanValue.True
                        : BooleanValue.False
                        ;
                }
            }

            return NilValue.Instance;
        }
    }
}
