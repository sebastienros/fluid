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
            var firstSegment = _segments[0];
            var task = firstSegment.ResolveFromScopeAsync(context);

            if (!task.IsCompletedSuccessfully)
            {
                return AwaitedFromScope(task, context, _segments);
            }

            var (value, useModelFallback) = task.Result;
            var start = 1;

            // Handle model fallback for IdentifierSegment when value is nil
            if (useModelFallback)
            {
                if (context.Model.IsNil())
                {
                    // Check equality as IsNil() is also true for UndefinedValue
                    if (context.Undefined is not null && value == UndefinedValue.Instance)
                    {
                        if (context.Options.StrictVariables)
                        {
                            throw new FluidException($"Undefined variable '{firstSegment.GetSegmentName()}'");
                        }
                        return context.Undefined.Invoke(firstSegment.GetSegmentName());
                    }
                    if (context.Options.StrictVariables)
                    {
                        throw new FluidException($"Undefined variable '{firstSegment.GetSegmentName()}'");
                    }
                    return new ValueTask<FluidValue>(value);
                }

                start = 0;
                value = context.Model;
            }

            for (var i = start; i < _segments.Length; i++)
            {
                var s = _segments[i];
                var resolveTask = s.ResolveAsync(value, context);

                if (!resolveTask.IsCompletedSuccessfully)
                {
                    return Awaited(resolveTask, context, _segments, i + 1);
                }

                value = resolveTask.Result;

                // Stop processing as soon as a member returns nothing
                if (value.IsNil())
                {
                    if (context.Options.StrictVariables)
                    {
                        throw new FluidException($"Undefined variable '{s.GetSegmentName()}'");
                    }
                    return new ValueTask<FluidValue>(value);
                }
            }

            return new ValueTask<FluidValue>(value);
        }

        private static async ValueTask<FluidValue> AwaitedFromScope(
            ValueTask<(FluidValue Value, bool UseModelFallback)> task,
            TemplateContext context,
            MemberSegment[] segments)
        {
            var (value, useModelFallback) = await task;
            var start = 1;

            // Handle model fallback for IdentifierSegment when value is nil
            if (useModelFallback)
            {
                if (context.Model.IsNil())
                {
                    if (context.Undefined is not null && value == UndefinedValue.Instance)
                    {
                        if (context.Options.StrictVariables)
                        {
                            throw new FluidException($"Undefined variable '{segments[0].GetSegmentName()}'");
                        }
                        return await context.Undefined.Invoke(segments[0].GetSegmentName());
                    }
                    if (context.Options.StrictVariables)
                    {
                        throw new FluidException($"Undefined variable '{segments[0].GetSegmentName()}'");
                    }
                    return value;
                }

                start = 0;
                value = context.Model;
            }

            for (var i = start; i < segments.Length; i++)
            {
                var s = segments[i];
                value = await s.ResolveAsync(value, context);

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
