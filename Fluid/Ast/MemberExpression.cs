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
                ExceptionHelper.ThrowArgumentException(nameof(segments), "At least one segment is required in a MemberExpression");
            }
        }

        public IReadOnlyList<MemberSegment> Segments => _segments;

        public override ValueTask<FluidValue> EvaluateAsync(TemplateContext context)
        {
            // The first segment can only be an IdentifierSegment

            var initial = _segments[0] as IdentifierSegment;

            // Search the initial segment in the local scope first

            var value = context.LocalScope.GetValue(initial.Identifier);

            // If it was not successful, try again with a member of the model

            var start = 1;

            if (value.IsNil())
            {
                // A context created without an explicit model uses NilValue.Instance.
                // Treat this as "no model" so undefined variables can be tracked / handled.
                if (context.Model.IsNil())
                {
                    // Check equality as IsNil() is also true for UndefinedValue
                    if (context.Undefined is not null && value == UndefinedValue.Instance)
                    {
                        if (context.Options.StrictVariables)
                        {
                            throw new FluidException($"Undefined variable '{initial.Identifier}'");
                        }
                        return context.Undefined.Invoke(initial.Identifier);
                    }
                    if (context.Options.StrictVariables)
                    {
                        throw new FluidException($"Undefined variable '{initial.Identifier}'");
                    }
                    return value;
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
                    if (context.Options.StrictVariables)
                    {
                        throw new FluidException($"Undefined variable '{s.GetSegmentName()}'");
                    }
                    return value;
                }
            }

            return value;
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
                    if (context.Options.StrictVariables)
                    {
                        throw new FluidException($"Undefined variable '{s.GetSegmentName()}'");
                    }
                    return value;
                }
            }

            return value;
        }

        protected internal override Expression Accept(AstVisitor visitor) => visitor.VisitMemberExpression(this);
    }
}
