using System.Threading.Tasks;
using Fluid.Values;

namespace Fluid.Ast.BinaryExpressions
{
    public class LowerThanExpression : BinaryExpression
    {
        public LowerThanExpression(Expression left, Expression right, bool strict) : base(left, right)
        {
            Strict = strict;
        }

        public bool Strict { get; }

        public override async ValueTask<FluidValue> EvaluateAsync(TemplateContext context)
        {
            var leftValue = await Left.EvaluateAsync(context);
            var rightValue = await Right.EvaluateAsync(context);

            if (leftValue.IsNil() || rightValue.IsNil())
            {
                if (Strict)
                {
                    return BooleanValue.False;
                }
                else
                {
                    return leftValue.IsNil() && rightValue.IsNil()
                        ? BooleanValue.True
                        : BooleanValue.False
                        ;
                }
            }

            if (leftValue is NumberValue)
            {
                if (Strict)
                {
                    return leftValue.ToNumberValue() < rightValue.ToNumberValue()
                        ? BooleanValue.True
                        : BooleanValue.False
                        ;
                }
                else
                {
                    return leftValue.ToNumberValue() <= rightValue.ToNumberValue()
                        ? BooleanValue.True
                        : BooleanValue.False
                        ;
                }
            }

            return NilValue.Instance;
        }
    }
}
