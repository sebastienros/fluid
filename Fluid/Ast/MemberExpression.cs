using Fluid.Values;
using Fluid.SourceGeneration;

namespace Fluid.Ast
{
    public sealed class MemberExpression : Expression, ISourceable
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
                        return context.Undefined.Invoke(firstSegment.GetSegmentName(), null);
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
                        return await context.Undefined.Invoke(segments[0].GetSegmentName(), null);
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

        public void WriteTo(SourceGenerationContext context)
        {
            var initial = (IdentifierSegment)_segments[0];
            var initialIdentifierLiteral = SourceGenerationContext.ToCSharpStringLiteral(initial.Identifier);

            context.WriteLine($"var value = {context.ContextName}.LocalScope.GetValue({initialIdentifierLiteral});");
            context.WriteLine("var start = 1;");
            context.WriteLine();
            context.WriteLine("if (value.IsNil())");
            context.WriteLine("{");
            using (context.Indent())
            {
                context.WriteLine($"if ({context.ContextName}.Model == null)");
                context.WriteLine("{");
                using (context.Indent())
                {
                    context.WriteLine("// Check equality as IsNil() is also true for UndefinedValue");
                    context.WriteLine($"if ({context.ContextName}.Undefined is not null && value == UndefinedValue.Instance)");
                    context.WriteLine("{");
                    using (context.Indent())
                    {
                        context.WriteLine($"if ({context.ContextName}.Options.StrictVariables)");
                        context.WriteLine("{");
                        using (context.Indent())
                        {
                            context.WriteLine($"throw new FluidException(\"Undefined variable '{initial.Identifier}'\");");
                        }
                        context.WriteLine("}");
                        context.WriteLine($"return await {context.ContextName}.Undefined.Invoke({initialIdentifierLiteral}, null);");
                    }
                    context.WriteLine("}");

                    context.WriteLine($"if ({context.ContextName}.Options.StrictVariables)");
                    context.WriteLine("{");
                    using (context.Indent())
                    {
                        context.WriteLine($"throw new FluidException(\"Undefined variable '{initial.Identifier}'\");");
                    }
                    context.WriteLine("}");

                    context.WriteLine("return value;");
                }
                context.WriteLine("}");

                context.WriteLine("start = 0;");
                context.WriteLine($"value = {context.ContextName}.Model;");
            }
            context.WriteLine("}");

            context.WriteLine();
            context.WriteLine($"for (var i = start; i < {_segments.Length}; i++)");
            context.WriteLine("{");
            using (context.Indent())
            {
                for (var i = 0; i < _segments.Length; i++)
                {
                    var segment = _segments[i];
                    context.WriteLine($"if (i == {i})");
                    context.WriteLine("{");
                    using (context.Indent())
                    {
                        if (segment is IdentifierSegment id)
                        {
                            var nameLit = SourceGenerationContext.ToCSharpStringLiteral(id.Identifier);
                            context.WriteLine($"value = await value.GetValueAsync({nameLit}, {context.ContextName});");
                            context.WriteLine("if (value.IsNil())");
                            context.WriteLine("{");
                            using (context.Indent())
                            {
                                context.WriteLine($"if ({context.ContextName}.Options.StrictVariables)");
                                context.WriteLine("{");
                                using (context.Indent())
                                {
                                    context.WriteLine($"throw new FluidException(\"Undefined variable '{id.Identifier}'\");");
                                }
                                context.WriteLine("}");
                                context.WriteLine("return value;");
                            }
                            context.WriteLine("}");
                        }
                        else if (segment is IndexerSegment idx)
                        {
                            var indexExpr = context.GetExpressionMethodName(idx.Expression);
                            context.WriteLine($"var indexValue = await {indexExpr}({context.ContextName});");
                            context.WriteLine($"value = await value.GetIndexAsync(indexValue, {context.ContextName});");
                            context.WriteLine("if (value.IsNil())");
                            context.WriteLine("{");
                            using (context.Indent())
                            {
                                context.WriteLine($"if ({context.ContextName}.Options.StrictVariables)");
                                context.WriteLine("{");
                                using (context.Indent())
                                {
                                    context.WriteLine("throw new FluidException(\"Undefined variable '[index]'\");");
                                }
                                context.WriteLine("}");
                                context.WriteLine("return value;");
                            }
                            context.WriteLine("}");
                        }
                        else if (segment is FunctionCallSegment call)
                        {
                            context.WriteLine("var arguments = new FunctionArguments();");
                            for (var a = 0; a < call.Arguments.Count; a++)
                            {
                                var arg = call.Arguments[a];
                                var argName = SourceGenerationContext.ToCSharpStringLiteral(arg.Name);
                                if (arg.Expression is null)
                                {
                                    context.WriteLine($"arguments.Add({argName}, NilValue.Instance);");
                                }
                                else
                                {
                                    var argExpr = context.GetExpressionMethodName(arg.Expression);
                                    context.WriteLine($"arguments.Add({argName}, await {argExpr}({context.ContextName}));");
                                }
                            }
                            context.WriteLine($"value = await value.InvokeAsync(arguments, {context.ContextName});");
                            context.WriteLine("if (value.IsNil())");
                            context.WriteLine("{");
                            using (context.Indent())
                            {
                                context.WriteLine($"if ({context.ContextName}.Options.StrictVariables)");
                                context.WriteLine("{");
                                using (context.Indent())
                                {
                                    context.WriteLine("throw new FluidException(\"Undefined variable '()'\");");
                                }
                                context.WriteLine("}");
                                context.WriteLine("return value;");
                            }
                            context.WriteLine("}");
                        }
                        else
                        {
                            SourceGenerationContext.ThrowNotSourceable(segment);
                        }
                    }
                    context.WriteLine("}");
                }
            }
            context.WriteLine("}");
            context.WriteLine();
            context.WriteLine("return value;");
        }
    }
}
