using Fluid.Values;
using Fluid.SourceGeneration;

namespace Fluid.Ast;

public sealed class MemberExpression : Expression, ISynchronousSourceable
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

        _identifiers = GetIdentifiers(_segments);
        _isSelf = _segments[0] is IdentifierSegment { Identifier: SelfValue.Name };
    }

    // Whether the variable is self, which is the self object when it isn't defined. It is only checked once a
    // variable is found undefined, so that defined variables are read as fast.
    private readonly bool _isSelf;

    // The segments when they are all identifiers, like a.b.c, which is the shape of nearly every member expression.
    // Typed as the sealed class, the calls to its members are direct.
    private readonly IdentifierSegment[] _identifiers;

    private static IdentifierSegment[] GetIdentifiers(MemberSegment[] segments)
    {
        var identifiers = new IdentifierSegment[segments.Length];

        for (var i = 0; i < segments.Length; i++)
        {
            if (segments[i] is not IdentifierSegment identifier)
            {
                return null;
            }

            identifiers[i] = identifier;
        }

        return identifiers;
    }

    public IReadOnlyList<MemberSegment> Segments => _segments;

    public override ValueTask<FluidValue> EvaluateAsync(TemplateContext context)
    {
        var identifiers = _identifiers;

        if (identifiers is null)
        {
            return EvaluateSegmentsAsync(context);
        }

        // Resolves the variable like IdentifierSegment.ResolveFromScopeAsync, without the virtual call and the tuple
        // it returns, which cost as much as the lookup itself.
        var name = identifiers[0].Identifier;
        var value = context.ShadowWithCounter(name, context.LocalScope.GetValue(name));

        if (value.IsNil())
        {
            return EvaluateFromModelAsync(value, context);
        }

        for (var i = 1; i < identifiers.Length; i++)
        {
            var segment = identifiers[i];
            var task = segment.ResolveAsync(value, context);

            if (!task.IsCompletedSuccessfully)
            {
                return Awaited(task, context, _segments, i + 1);
            }

            value = task.Result;

            if (value.IsNil())
            {
                if (context.Options.StrictVariables)
                {
                    throw new FluidException($"Undefined variable '{segment.Identifier}'");
                }

                return new ValueTask<FluidValue>(value);
            }
        }

        return new ValueTask<FluidValue>(value);
    }

    /// <summary>
    /// Evaluates the expression when its variable is not defined: its members are read from the model, if there is one.
    /// </summary>
    private ValueTask<FluidValue> EvaluateFromModelAsync(FluidValue value, TemplateContext context)
    {
        // Like Liquid, a variable named self, even nil, takes precedence over the self object.
        if (_isSelf && value == UndefinedValue.Instance)
        {
            return EvaluateSelfAsync(context);
        }

        if (context.Model.IsNil())
        {
            // Check equality as IsNil() is also true for UndefinedValue
            if (context.Undefined is not null && value == UndefinedValue.Instance)
            {
                if (context.Options.StrictVariables)
                {
                    throw new FluidException($"Undefined variable '{_segments[0].GetSegmentName()}'");
                }
                return context.Undefined.Invoke(_segments[0].GetSegmentName(), null);
            }
            if (context.Options.StrictVariables)
            {
                throw new FluidException($"Undefined variable '{_segments[0].GetSegmentName()}'");
            }
            return new ValueTask<FluidValue>(value);
        }

        return ResolveSegmentsAsync(context.Model, 0, context);
    }

    /// <summary>
    /// Evaluates the expression when its variable is self and not defined: the model's self member, or else the self object.
    /// </summary>
    private ValueTask<FluidValue> EvaluateSelfAsync(TemplateContext context)
    {
        if (context.Model.IsNil())
        {
            return ResolveSegmentsAsync(GetSelf(context), 1, context);
        }

        var task = context.Model.GetValueAsync(SelfValue.Name, context);

        if (task.IsCompletedSuccessfully)
        {
            return ResolveSegmentsAsync(task.Result.IsNil() ? GetSelf(context) : task.Result, 1, context);
        }

        return Awaited(task, context);

        async ValueTask<FluidValue> Awaited(ValueTask<FluidValue> task, TemplateContext context)
        {
            var value = await task;
            return await ResolveSegmentsAsync(value.IsNil() ? GetSelf(context) : value, 1, context);
        }
    }

    // Reading a member of self, like self.title, reads the variable of the current template without creating the object.
    private SelfValue GetSelf(TemplateContext context) => _segments.Length == 1 ? SelfValue.Create(context) : SelfValue.Current;

    private ValueTask<FluidValue> ResolveSegmentsAsync(FluidValue value, int start, TemplateContext context)
    {
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

    private ValueTask<FluidValue> EvaluateSegmentsAsync(TemplateContext context)
    {
        var firstSegment = _segments[0];
        var task = firstSegment.ResolveFromScopeAsync(context);

        if (!task.IsCompletedSuccessfully)
        {
            return AwaitedFromScope(task, context, _segments);
        }

        var (value, useModelFallback) = task.Result;

        // Handle model fallback for IdentifierSegment when value is nil
        return useModelFallback
            ? EvaluateFromModelAsync(value, context)
            : ResolveSegmentsAsync(value, 1, context);
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

    // A path of identifiers, like a.b.c, is evaluated by an expression that the generated code builds once.
    bool ISynchronousSourceable.IsSynchronous => _segments.All(s => s is IdentifierSegment);

    public void WriteTo(SourceGenerationContext context)
    {
        if (((ISynchronousSourceable)this).IsSynchronous)
        {
            // The interpreter's evaluation completes synchronously when the members do, and its segments cache the
            // accessors they resolve. Each call site gets its own expression, and so its own caches.
            var segments = string.Join(", ", _segments.Select(s => $"new global::Fluid.Ast.IdentifierSegment({SourceGenerationContext.ToCSharpStringLiteral(((IdentifierSegment)s).Identifier)})"));
            var field = context.AddStaticField("global::Fluid.Ast.MemberExpression", "Member_", $"new global::Fluid.Ast.MemberExpression(new global::Fluid.Ast.MemberSegment[] {{ {segments} }})");
            context.WriteLine($"return {field}.EvaluateAsync({context.ContextName});");
            return;
        }

        if (_segments[0] is IndexerSegment rootIndexer)
        {
            // A variable whose name is computed, like {{ [name] }}, resolved like IndexerSegment.ResolveFromScopeAsync.
            var keyExpr = context.GetExpressionMethodName(rootIndexer.Expression);
            context.WriteLine($"var key = (await {keyExpr}({context.ContextName})).ToStringValue({context.ContextName});");
            context.WriteLine($"var value = {context.ContextName}.ShadowWithCounter(key, {context.ContextName}.LocalScope.GetValue(key));");
            context.WriteLine("var start = 1;");
            context.WriteLine($"if (value.IsNil() && !{context.ContextName}.Model.IsNil())");
            context.WriteLine("{");
            using (context.Indent())
            {
                context.WriteLine($"value = await {context.ContextName}.Model.GetValueAsync(key, {context.ContextName});");
            }
            context.WriteLine("}");

            WriteSegmentsTo(context);
            return;
        }

        if (_segments[0] is not IdentifierSegment initial)
        {
            SourceGenerationContext.ThrowNotSourceable(_segments[0]);
            return;
        }

        var initialIdentifierLiteral = SourceGenerationContext.ToCSharpStringLiteral(initial.Identifier);

        context.WriteLine($"var value = {context.ContextName}.ShadowWithCounter({initialIdentifierLiteral}, {context.ContextName}.LocalScope.GetValue({initialIdentifierLiteral}));");
        context.WriteLine("var start = 1;");
        context.WriteLine();

        if (_isSelf)
        {
            // Like EvaluateSelfAsync: an undefined self is the model's self member, or else the self object.
            context.WriteLine("if (value == UndefinedValue.Instance)");
            context.WriteLine("{");
            using (context.Indent())
            {
                context.WriteLine($"value = {context.ContextName}.Model.IsNil() ? NilValue.Instance : await {context.ContextName}.Model.GetValueAsync({initialIdentifierLiteral}, {context.ContextName});");
                context.WriteLine("if (value.IsNil())");
                context.WriteLine("{");
                using (context.Indent())
                {
                    context.WriteLine($"value = SelfValue.Create({context.ContextName});");
                }
                context.WriteLine("}");
            }
            context.WriteLine("}");
            context.WriteLine();
        }

        context.WriteLine("if (value.IsNil())");
        context.WriteLine("{");
        using (context.Indent())
        {
            context.WriteLine($"if ({context.ContextName}.Model.IsNil())");
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

            context.WriteLine("else");
            context.WriteLine("{");
            using (context.Indent())
            {
                context.WriteLine("start = 0;");
                context.WriteLine($"value = {context.ContextName}.Model;");
            }
            context.WriteLine("}");
        }
        context.WriteLine("}");

        WriteSegmentsTo(context);
    }

    private void WriteSegmentsTo(SourceGenerationContext context)
    {
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
                        // A segment per call site caches the accessor it resolves, like the interpreter's.
                        var segmentField = context.AddStaticField("global::Fluid.Ast.IdentifierSegment", "Segment_", $"new global::Fluid.Ast.IdentifierSegment({SourceGenerationContext.ToCSharpStringLiteral(id.Identifier)})");
                        context.WriteLine($"value = await {segmentField}.ResolveAsync(value, {context.ContextName});");
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
                        context.WriteLine($"value = value is NumberValue ? NilValue.Instance : await value.GetIndexAsync(indexValue, {context.ContextName});");
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
