using System.Collections.Generic;
using System.Threading.Tasks;
using Fluid.Values;

namespace Fluid.Ast
{
    internal sealed class MemberExpression : Expression
    {
        private readonly List<MemberSegment> _segments;

        public MemberExpression(params MemberSegment[] segments) : this(new List<MemberSegment>(segments))
        {
        }

        public MemberExpression(List<MemberSegment> segments)
        {
            _segments = segments;
        }

        public IReadOnlyList<MemberSegment> Segments => _segments;

        public override ValueTask<FluidValue> EvaluateAsync(TemplateContext context)
        {
            // The first segment can only be an IdentifierSegment

            var initial = _segments[0] as IdentifierSegment;

            // Search the initial segment in the local scope first

            FluidValue value = context.LocalScope.GetValue(initial.Identifier);

            // If it was not successful, try again with a member of the model

            int start = 1;

            if (value.IsNil())
            {
                if (context.Model == null)
                {
                    return new ValueTask<FluidValue>(value);
                }

                start = 0;
                value = context.Model;
            }

            var i = start;
            foreach (var s in _segments.AsSpan(start))
            {
                var task = s.ResolveAsync(value, context);

                if (!task.IsCompletedSuccessfully)
                {
                    return Awaited(task, context, _segments, i + 1);
                }

                value = task.Result;

                // Stop processing as soon as a member returns nothing
                if (value.IsNil())
                {
                    return new ValueTask<FluidValue>(value);
                }

                i++;
            }

            return new ValueTask<FluidValue>(value);
        }

        private static async ValueTask<FluidValue> Awaited(
            ValueTask<FluidValue> task,
            TemplateContext context,
            List<MemberSegment> segments,
            int startIndex)
        {
            var value = await task;
            for (var i = startIndex; i < segments.Count; i++)
            {
                var s = segments[i];
                value = await s.ResolveAsync(value, context);

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
