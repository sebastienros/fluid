using System.Threading.Tasks;
using Fluid.Values;

namespace Fluid.Ast
{
    internal sealed class IndexerSegment : MemberSegment
    {
        private readonly Expression _expression;

        public IndexerSegment(Expression expression)
        {
            _expression = expression;
        }

        public override async ValueTask<FluidValue> ResolveAsync(FluidValue value, TemplateContext context)
        {
            var index = await _expression.EvaluateAsync(context);
            return await value.GetIndexAsync(index, context);
        }
    }
}
