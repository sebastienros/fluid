using Fluid.Values;

namespace Fluid.Ast
{
    public sealed class MemberExpression : Expression
    {
        private readonly MemberSegment[] _segments;

        public MemberExpression(MemberSegment segment) : this([segment])
        {
        }

        public MemberExpression(IReadOnlyList<MemberSegment> segments) : this(segments as MemberSegment[] ?? segments.ToArray())
        {
        }

        internal MemberExpression(MemberSegment[] segments)
        {
            _segments = segments ?? [];

            if (_segments.Length == 0)
            {
                ExceptionHelper.ThrowArgumentNullException(nameof(segments), "At least one segment is required in a MemberExpression");
            }
        }

        public IReadOnlyList<MemberSegment> Segments => _segments;

        public override ValueTask<FluidValue> EvaluateAsync(TemplateContext context)
        {
            // The first segment can only be an IdentifierSegment

            var initial = _segments[0] as IdentifierSegment;

            // Search the initial segment in the local scope first

            var value = context.LocalScope.GetValue(initial.Identifier, context);

            // If it was not successful, try again with a member of the model

            var start = 1;

            if (value.IsNil())
            {
                if (context.Model == null)
                {
                    return new ValueTask<FluidValue>(value);
                }

                start = 0;
                value = context.Model;
            }

            for (var i = start; i < _segments.Length; i++)
            {
                var s = _segments[i];
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
                value = await s.ResolveAsync(value, context);

                // Stop processing as soon as a member returns nothing
                if (value.IsNil())
                {
                    return value;
                }
            }

            return value;
        }

        protected internal override Expression Accept(AstVisitor visitor) => visitor.VisitMemberExpression(this);
    }
}
