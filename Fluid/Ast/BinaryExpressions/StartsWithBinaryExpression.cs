using Fluid.Values;
using System.Threading.Tasks;

namespace Fluid.Ast.BinaryExpressions
{
    public class StartsWithBinaryExpression : BinaryExpression
    {
        public StartsWithBinaryExpression(Expression left, Expression right) : base(left, right)
        {
        }

        public override async ValueTask<FluidValue> EvaluateAsync(TemplateContext context)
        {
            var leftValue = await Left.EvaluateAsync(context);
            var rightValue = await Right.EvaluateAsync(context);

            if (leftValue is ArrayValue)
            {
                var first = await leftValue.GetValueAsync("first", context);
                return first.Equals(rightValue)
                        ? BooleanValue.True
                        : BooleanValue.False;
            }
            else
            {
                return leftValue.ToStringValue().StartsWith(rightValue.ToStringValue())
                        ? BooleanValue.True
                        : BooleanValue.False;
            }
        }
    }
}
