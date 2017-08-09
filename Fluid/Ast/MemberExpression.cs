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
                if (value == null)
                {
                    // Root property
                    value = await segment.ResolveAsync(context.LocalScope, context);
                }
                else
                {
                    value = await segment.ResolveAsync(value, context);
                }

                // Stop processing as soon as a member returns nothing
                if (value.IsNil())
                {
                    return value;
                }
            }

            return value;
        }
    }
}
