using Fluid.Ast.BinaryExpressions;
using Fluid.Values;
using System;
using System.Runtime.CompilerServices;
using System.Threading.Tasks;

namespace Fluid.Ast
{
    public class RangeExpression : Expression
    {
        public RangeExpression(Expression from, Expression to)
        {
            From = from;
            To = to;
        }

        public Expression From { get; }

        public Expression To { get; }

        public override ValueTask<FluidValue> EvaluateAsync(TemplateContext context)
        {
            int start, end;

            var startTask = From.EvaluateAsync(context);
            var endTask = To.EvaluateAsync(context);

            if (startTask.IsCompletedSuccessfully && endTask.IsCompletedSuccessfully)
            {
                start = Convert.ToInt32(startTask.Result.ToNumberValue());
                end = Convert.ToInt32(endTask.Result.ToNumberValue());

                // If end < start, we create an empty array
                var list = new FluidValue[Math.Max(0, end - start + 1)];

                for (var i = 0; i < list.Length; i++)
                {
                    list[i] = NumberValue.Create(start + i);
                }

                return new ArrayValue(list);
            }
            else
            {
                return Awaited(startTask, endTask);
            }            
        }

        [MethodImpl(MethodImplOptions.NoInlining)]
        private async ValueTask<FluidValue> Awaited(
            ValueTask<FluidValue> leftTask,
            ValueTask<FluidValue> rightTask)
        {
            var start = Convert.ToInt32((await leftTask).ToNumberValue());
            var end = Convert.ToInt32((await rightTask).ToNumberValue());

            var list = new FluidValue[Math.Max(1, end - start)];

            for (var i = start; i <= end; i++)
            {
                list[i] = NumberValue.Create(i);
            }

            return new ArrayValue(list);
        }
    }
}
