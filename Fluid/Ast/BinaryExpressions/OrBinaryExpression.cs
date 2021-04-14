using System.Threading.Tasks;
using Fluid.Values;

namespace Fluid.Ast.BinaryExpressions
{
    public class OrBinaryExpression : BinaryExpression
    {
        public OrBinaryExpression(Expression left, Expression right) : base(left, right)
        {
        }

        public override ValueTask<FluidValue> EvaluateAsync(TemplateContext context)
        {
            static async ValueTask<FluidValue> Awaited(ValueTask<FluidValue> leftTask, ValueTask<FluidValue> rightTask)
            {
                var leftValue = await leftTask;
                var rightValue = await rightTask;

                return BooleanValue.Create(leftValue.ToBooleanValue() || rightValue.ToBooleanValue());
            }

            var leftTask = Left.EvaluateAsync(context);
            var rightTask = Right.EvaluateAsync(context);

            if (leftTask.IsCompletedSuccessfully && rightTask.IsCompletedSuccessfully)
            {
                return BooleanValue.Create(leftTask.Result.ToBooleanValue() || rightTask.Result.ToBooleanValue());
            }

            return Awaited(leftTask, rightTask);
        }
    }
}
