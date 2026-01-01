using Fluid.Values;

namespace Fluid.Ast
{
    public sealed class IndexerSegment : MemberSegment
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

        public override string GetSegmentName()
        {
            // For indexer segments, return a representation like [index]
            // Since we don't have the evaluated value here, we return a generic representation
            return "[index]";
        }
    }
}
