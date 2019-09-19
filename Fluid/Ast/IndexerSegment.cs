using System.Threading.Tasks;
using Fluid.Values;

namespace Fluid.Ast
{
    public class IndexerSegment : MemberSegment
    {
        public IndexerSegment(Expression expression)
        {
            Expression = expression;
        }

        public Expression Expression { get; }

        public override async ValueTask<FluidValue> ResolveAsync(FluidValue value, TemplateContext context)
        {
            var index = await Expression.EvaluateAsync(context);
            return await value.GetIndexAsync(index, context);
        }

        public override ValueTask<FluidValue> ResolveAsync(Scope value, TemplateContext context)
        {
            static async ValueTask<FluidValue> Awaited(ValueTask<FluidValue> valueTask, Scope s)
            {
                var index = await valueTask;
                return s.GetIndex(index);
            }

            var task = Expression.EvaluateAsync(context);
            if (task.IsCompletedSuccessfully)
            {
                return new ValueTask<FluidValue>(value.GetIndex(task.Result));
            }

            return Awaited(task, value);
        }
    }
}
