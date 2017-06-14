using System.Threading.Tasks;
using Fluid.Values;

namespace Fluid.Ast.BinaryExpressions
{
    public class AddBinaryExpression : BinaryExpression
    {
        public AddBinaryExpression(Expression left, Expression right) : base(left, right)
        {
        }

        public override async Task<FluidValue> EvaluateAsync(TemplateContext context)
        {
            var leftValue = await Left.EvaluateAsync(context);
            var rightValue = await Right.EvaluateAsync(context);
            
            if (leftValue is StringValue)
            {
                return new StringValue(leftValue.ToStringValue() + rightValue.ToStringValue());
            }

            if (leftValue is NumberValue)
            {
                return new NumberValue(leftValue.ToNumberValue() + rightValue.ToNumberValue());
            }

            return NilValue.Instance;
        }
    }
}
