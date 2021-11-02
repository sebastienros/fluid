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
    }
}
