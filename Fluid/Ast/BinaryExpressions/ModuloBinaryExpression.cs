using System.Threading.Tasks;
using Fluid.Values;

namespace Fluid.Ast.BinaryExpressions
{
    public class ModuloBinaryExpression : BinaryExpression
    {
        public ModuloBinaryExpression(Expression left, Expression right) : base(left, right)
        {
        }

        public override async ValueTask<FluidValue> EvaluateAsync(TemplateContext context)
        {
            var leftValue = await Left.EvaluateAsync(context);
            var rightValue = await Right.EvaluateAsync(context);

            if (leftValue is NumberValue && rightValue is NumberValue)
            {
                return NumberValue.Create(leftValue.ToNumberValue() % rightValue.ToNumberValue());
            }

            return NilValue.Instance;
        }
    }
}
