using System.Threading.Tasks;
using Fluid.Values;

namespace Fluid.Ast.BinaryExpressions
{
    public class OrBinaryExpression : BinaryExpression
    {
        public OrBinaryExpression(Expression left, Expression right) : base(left, right)
        {
        }

        public override async Task<FluidValue> EvaluateAsync(TemplateContext context)
        {
            var leftValue = await Left.EvaluateAsync(context);
            var rightValue = await Right.EvaluateAsync(context);

            return new BooleanValue(leftValue.ToBooleanValue() || rightValue.ToBooleanValue());
        }
    }
}
