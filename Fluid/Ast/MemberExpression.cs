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

        public override ValueTask<FluidValue> EvaluateAsync(TemplateContext context)
        {
            FluidValue value = null;

            for (var i = 0; i < Segments.Length; i++)
            {
                var s = Segments[i];
                var task = value == null
                    ? s.ResolveAsync(context.LocalScope, context) // root
                    : s.ResolveAsync(value, context);

                if (!task.IsCompletedSuccessfully)
                {
                    return Awaited(task, context, Segments, i + 1);
                }

                value = task.Result;
                // Stop processing as soon as a member returns nothing
                if (value.IsNil())
                {
                    return new ValueTask<FluidValue>(value);
                }
            }

            return new ValueTask<FluidValue>(value);
        }

        private static async ValueTask<FluidValue> Awaited(
            ValueTask<FluidValue> task,
            TemplateContext context,
            MemberSegment[] segments,
            int startIndex)
        {
            var value = await task;
            for (var i = startIndex; i < segments.Length; i++)
            {
                var s = segments[i];
                value = await (value == null
                    ? s.ResolveAsync(context.LocalScope, context) // root
                    : s.ResolveAsync(value, context));

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
