using Fluid.Ast;
using Fluid.Values;
using System.Threading.Tasks;

namespace Fluid.Tests.Extensibility
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

            return leftValue.ToStringValue().StartsWith(rightValue.ToStringValue())
                    ? BooleanValue.True
                    : BooleanValue.False;
        }
    }
}
