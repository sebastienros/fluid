using System.Threading.Tasks;
using Fluid.Values;

namespace Fluid.Ast.BinaryExpressions
{
    public class AndBinaryExpression : BinaryExpression
    {
        public AndBinaryExpression(Expression left, Expression right) : base(left, right)
        {
        }

        public override async ValueTask<FluidValue> EvaluateAsync(TemplateContext context)
        {
            var leftValue = await Left.EvaluateAsync(context);
            var rightValue = await Right.EvaluateAsync(context);

            return new BooleanValue(leftValue.ToBooleanValue() && rightValue.ToBooleanValue());
        }
    }
}
