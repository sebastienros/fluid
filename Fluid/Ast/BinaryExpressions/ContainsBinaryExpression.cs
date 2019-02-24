using System.Threading.Tasks;
using Fluid.Values;

namespace Fluid.Ast.BinaryExpressions
{
    public class ContainsBinaryExpression : BinaryExpression
    {
        public ContainsBinaryExpression(Expression left, Expression right) : base(left, right)
        {
        }

        public override async ValueTask<FluidValue> EvaluateAsync(TemplateContext context)
        {
            var leftValue = await Left.EvaluateAsync(context);
            var rightValue = await Right.EvaluateAsync(context);

            return leftValue.Contains(rightValue)
                    ? BooleanValue.True
                    : BooleanValue.False;
        }
    }
}
