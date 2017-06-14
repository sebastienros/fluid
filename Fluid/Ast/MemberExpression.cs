using System.Threading.Tasks;
using Fluid.Values;

namespace Fluid.Ast
{
    public class MemberExpression : Expression
    {
        public MemberExpression(params MemberSegment[] segments)
        {
            Segments = segments;
        }

        public MemberSegment[] Segments { get; }

        public override async Task<FluidValue> EvaluateAsync(TemplateContext context)
        {
            FluidValue value = null;

            foreach(var segment in Segments)
            {
                value = value != null
                    ? await segment.ResolveAsync(value, context)
                    : await segment.ResolveAsync(context.LocalScope, context);

                // Stop processing as soon as a member returns nothing
                if (value.IsUndefined())
                {
                    return value;
                }
            }

            return value;
        }
    }
}
