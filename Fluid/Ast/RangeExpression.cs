using Fluid.Compilation;
using Fluid.Values;
using System.Runtime.CompilerServices;

namespace Fluid.Ast
{
    public class RangeExpression : Expression
    {
        private FluidValue _cached = NilValue.Instance;

        public RangeExpression(Expression from, Expression to)
        {
            From = from;
            To = to;
        }

        public Expression From { get; }

        public Expression To { get; }

        public override ValueTask<FluidValue> EvaluateAsync(TemplateContext context)
        {
            if (_cached != NilValue.Instance)
            {
                return _cached;
            }

            int start, end;

            var startTask = From.EvaluateAsync(context);
            var endTask = To.EvaluateAsync(context);

            if (startTask.IsCompletedSuccessfully && endTask.IsCompletedSuccessfully)
            {
                start = Convert.ToInt32(startTask.Result.ToNumberValue());
                end = Convert.ToInt32(endTask.Result.ToNumberValue());

                var result = BuildArray(start, end);

                // If both expressions are constant the range can be cached
                
                if (From.IsConstantExpression() && To.IsConstantExpression()) 
                {
                    _cached = result;
                }

                return result;
            }
            else
            {
                return Awaited(startTask, endTask);
            }            
        }

        private static FluidValue BuildArray(int start, int end)
        {
            // If end < start, we create an empty array
            var list = new FluidValue[Math.Max(0, end - start + 1)];

            for (var i = 0; i < list.Length; i++)
            {
                list[i] = NumberValue.Create(start + i);
            }

            return new ArrayValue(list);
        }

        [MethodImpl(MethodImplOptions.NoInlining)]
        private async ValueTask<FluidValue> Awaited(
            ValueTask<FluidValue> leftTask,
            ValueTask<FluidValue> rightTask)
        {
            var start = Convert.ToInt32((await leftTask).ToNumberValue());
            var end = Convert.ToInt32((await rightTask).ToNumberValue());

            return BuildArray(start, end);
        }
    }
}
