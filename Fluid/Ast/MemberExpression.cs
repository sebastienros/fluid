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
                ExceptionHelper.ThrowArgumentNullException(nameof(segments), "At least one segment is required in a MemberExpression");
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
                if (context.Model == null)
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
                        context.WriteLine($"return await {context.ContextName}.Undefined.Invoke({initialIdentifierLiteral});");
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
