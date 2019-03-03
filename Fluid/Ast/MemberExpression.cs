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

        public override async ValueTask<FluidValue> EvaluateAsync(TemplateContext context)
        {
            FluidValue value = null;

            var length = Segments.Length;

            for (var i = 0; i< length; i++)
            {
                if (value == null)
                {
                    // Root property
                    value = await Segments[i].ResolveAsync(context.LocalScope, context);
                }
                else
                {
                    value = await Segments[i].ResolveAsync(value, context);
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
