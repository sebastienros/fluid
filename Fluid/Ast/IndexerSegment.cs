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

            //Selz: Support dynamic load properties from Array e.g. categoriesDrop["featured"]
            if (value is ReplayArrayValue replayArrayValue && index.Type == FluidValues.String)
            {
                var originalValue = new ObjectValue(replayArrayValue.OriginalValue);
                return await originalValue.GetIndexAsync(index, context);
            }

            return await value.GetIndexAsync(index, context);
        }

        public override async ValueTask<FluidValue> ResolveAsync(Scope value, TemplateContext context)
        {
            var index = await Expression.EvaluateAsync(context);
            return value.GetIndex(index);
        }
    }
}
