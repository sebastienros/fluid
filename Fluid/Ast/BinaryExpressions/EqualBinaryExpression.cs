using System.Threading.Tasks;
using Fluid.Values;

namespace Fluid.Ast.BinaryExpressions
{
    public class EqualBinaryExpression : BinaryExpression
    {
        public EqualBinaryExpression(Expression left, Expression right) : base(left, right)
        {
        }

        public override async Task<FluidValue> EvaluateAsync(TemplateContext context)
        {
            var leftValue = await Left.EvaluateAsync(context);
            var rightValue = await Right.EvaluateAsync(context);

            if (leftValue.Equals(rightValue))
            {
                return BooleanValue.True;
            }

            return BooleanValue.False;
        }
    }
}
