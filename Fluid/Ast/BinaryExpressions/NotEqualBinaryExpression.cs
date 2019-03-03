using System.Threading.Tasks;
using Fluid.Values;

namespace Fluid.Ast.BinaryExpressions
{
    public class NotEqualBinaryExpression : BinaryExpression
    {
        public NotEqualBinaryExpression(Expression left, Expression right) : base(left, right)
        {
        }

        public override async ValueTask<FluidValue> EvaluateAsync(TemplateContext context)
        {
            var leftValue = await Left.EvaluateAsync(context);
            var rightValue = await Right.EvaluateAsync(context);

            if (leftValue.Equals(rightValue))
            {
                return BooleanValue.False;
            }

            return BooleanValue.True;
        }
    }
}
