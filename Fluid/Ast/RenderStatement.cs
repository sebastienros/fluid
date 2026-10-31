using Fluid.Values;
using System.Text.Encodings.Web;
using Fluid.SourceGeneration;

namespace Fluid.Ast;

/// <summary>
/// The render tag can only access immutable environments, which means the scope of the context that was passed to the main template, global values, and the model.
/// </summary>
#pragma warning disable CA1001 // Types that own disposable fields should be disposable
public sealed class RenderStatement : Statement, ISourceable
#pragma warning restore CA1001
{
    public const string ViewExtension = ".liquid";

    public RenderStatement(FluidParser parser, string path, Expression with = null, Expression @for = null, string alias = null, IReadOnlyList<AssignStatement> assignStatements = null)
    {
        Parser = parser;
        Path = path;
        With = with;
        For = @for;
        Alias = alias;
        AssignStatements = assignStatements ?? [];
    }

    public FluidParser Parser { get; }
    public string Path { get; }
    public IReadOnlyList<AssignStatement> AssignStatements { get; }
    public Expression With { get; }
    public Expression For { get; }
    public string Alias { get; }

    public override ValueTask<Completion> WriteToAsync(IFluidOutput output, TextEncoder encoder, TemplateContext context)
    {
        if (With != null || For != null || AssignStatements.Count > 0)
        {
            return WriteToAsyncCore(output, encoder, context);
        }

        context.IncrementSteps();

        var task = TemplateLoader.LoadAsync(
            Parser,
            Path,
            context,
            context.Options.DefaultFileExtension);

        if (task.IsCompletedSuccessfully)
        {
            return RenderLoadedTemplate(task.Result.Template, output, encoder, context);
        }

        return AwaitedLoad(task, output, encoder, context);

        static async ValueTask<Completion> AwaitedLoad(
            ValueTask<TemplateLoader.LoadedTemplate> task,
            IFluidOutput output,
            TextEncoder encoder,
            TemplateContext context)
        {
            var loadedTemplate = await task;
            return await RenderLoadedTemplate(loadedTemplate.Template, output, encoder, context);
        }
    }

    private static ValueTask<Completion> RenderLoadedTemplate(
        IFluidTemplate template,
        IFluidOutput output,
        TextEncoder encoder,
        TemplateContext context)
    {
        var scope = context.EnterScope(ScopeBehavior.Isolated);

        try
        {
            var task = template.RenderAsync(output, encoder, context);
            if (task.IsCompletedSuccessfully)
            {
                scope.Dispose();
                return NormalCompletion;
            }

            return AwaitedRender(task, scope);
        }
        catch
        {
            scope.Dispose();
            throw;
        }

        static async ValueTask<Completion> AwaitedRender(
            ValueTask task,
            TemplateContext.ScopeLease scope)
        {
            try
            {
                await task;
                return Completion.Normal;
            }
            finally
            {
                scope.Dispose();
            }
        }
    }

    private async ValueTask<Completion> WriteToAsyncCore(IFluidOutput output, TextEncoder encoder, TemplateContext context)
    {
        context.IncrementSteps();

        var relativePath = Path;
        var loadedTemplate = await TemplateLoader.LoadAsync(
            Parser,
            relativePath,
            context,
            context.Options.DefaultFileExtension);
        relativePath = loadedTemplate.Path;
        var template = loadedTemplate.Template;

        var identifier = System.IO.Path.GetFileNameWithoutExtension(relativePath);

        FluidValue withValue = null;
        IFluidArray list = null;

        if (With != null)
        {
            withValue = await With.EvaluateAsync(context);
        }
        else if (For != null)
        {
            var evaluatedFor = await For.EvaluateAsync(context);

            // Like Liquid, only a collection is looped over: any other value is rendered once, like with.
            if (IsCollection(evaluatedFor))
            {
                list = await ArrayValue.ToFluidArrayAsync(evaluatedFor, context);
            }
            else
            {
                withValue = evaluatedFor;
            }
        }

        // Liquid evaluates named argument expressions in the caller before creating the isolated context.
        var assignedValues = await EvaluateAssignStatementsAsync(AssignStatements, context);

        using var scope = context.EnterScope(ScopeBehavior.Isolated);

        if (With != null)
        {
            context.SetValue(Alias ?? identifier, withValue);
            ApplyAssignStatements(assignedValues, context);
            await template.RenderAsync(output, encoder, context);
        }
        else if (For != null && list == null)
        {
            ApplyAssignStatements(assignedValues, context);
            context.SetValue(Alias ?? identifier, withValue);
            await template.RenderAsync(output, encoder, context);
        }
        else if (For != null)
        {
            var forloop = ForLoopValue.Create();
            forloop.IsRenderLoop = true;
            var length = forloop.Length = list.Count;

            for (var i = 0; i < length; i++)
            {
                context.IncrementSteps();

                // Like Liquid, each item is rendered in its own isolated context: the variables and
                // counters of the previous items are gone.
                using var itemScope = context.EnterScope(ScopeBehavior.Isolated);

                ApplyAssignStatements(assignedValues, context);
                context.SetValue("forloop", forloop);
                context.SetValue(Alias ?? identifier, list[i]);

                forloop.Index = i + 1;
                forloop.Index0 = i;
                forloop.RIndex = length - i;
                forloop.RIndex0 = length - i - 1;
                forloop.First = i == 0;
                forloop.Last = i == length - 1;

                await template.RenderAsync(output, encoder, context);
            }
        }
        else
        {
            ApplyAssignStatements(assignedValues, context);
            await template.RenderAsync(output, encoder, context);
        }

        return Completion.Normal;
    }

    protected internal override Statement Accept(AstVisitor visitor) => visitor.VisitRenderStatement(this);

    public void WriteTo(SourceGenerationContext context)
    {
        var assignedValueNames = new string[AssignStatements.Count];

        void EmitEvaluateAssignStatements()
        {
            for (var i = 0; i < AssignStatements.Count; i++)
            {
                var assignStatement = AssignStatements[i];
                var valueExpr = context.GetExpressionMethodName(assignStatement.Value);
                var valueName = context.GetUniqueId("assignedValue");
                assignedValueNames[i] = valueName;

                context.WriteLine($"{context.ContextName}.IncrementSteps();");
                context.WriteLine($"var {valueName} = await {valueExpr}({context.ContextName});");
                context.WriteLine($"if ({context.ContextName}.Assigned != null)");
                context.WriteLine("{");
                using (context.Indent())
                {
                    context.WriteLine($"{valueName} = await {context.ContextName}.Assigned.Invoke({SourceGenerationContext.ToCSharpStringLiteral(assignStatement.Identifier)}, {valueName}, {context.ContextName});");
                }
                context.WriteLine("}");
            }
        }

        void EmitApplyAssignStatements()
        {
            for (var i = 0; i < AssignStatements.Count; i++)
            {
                var assignStatement = AssignStatements[i];
                context.WriteLine($"{context.ContextName}.SetValue({SourceGenerationContext.ToCSharpStringLiteral(assignStatement.Identifier)}, {assignedValueNames[i]});");
            }
        }

        // The referenced template is compiled ahead-of-time and resolved by path.
        var templateTypeName = context.GetRenderTemplateTypeName(Path);

        context.WriteLine($"{context.ContextName}.IncrementSteps();");
        context.WriteLine($"var template = new {templateTypeName}();");

        // Use the same default identifier logic as runtime (file name without extension).
        context.WriteLine($"var identifier = System.IO.Path.GetFileNameWithoutExtension({SourceGenerationContext.ToCSharpStringLiteral(Path)});");

        if (With != null)
        {
            var withExpr = context.GetExpressionMethodName(With);
            context.WriteLine($"var withValue = await {withExpr}({context.ContextName});");
        }
        else if (For != null)
        {
            var forExpr = context.GetExpressionMethodName(For);
            context.WriteLine($"var evaluatedFor = await {forExpr}({context.ContextName});");
            context.WriteLine($"var list = evaluatedFor.Type == FluidValues.Array || evaluatedFor.Type == FluidValues.Dictionary ? await ArrayValue.ToFluidArrayAsync(evaluatedFor, {context.ContextName}) : null;");
        }

        EmitEvaluateAssignStatements();
        context.WriteLine($"using var scope = {context.ContextName}.EnterScope(ScopeBehavior.Isolated);");

        if (With != null)
        {
            if (!string.IsNullOrEmpty(Alias))
            {
                context.WriteLine($"{context.ContextName}.SetValue({SourceGenerationContext.ToCSharpStringLiteral(Alias)}, withValue);");
            }
            else
            {
                context.WriteLine($"{context.ContextName}.SetValue(identifier, withValue);");
            }

            EmitApplyAssignStatements();
            context.WriteLine($"await template.RenderAsync({context.WriterName}, {context.EncoderName}, {context.ContextName});");
        }
        else if (For != null)
        {
            // Like the interpreter, a value that isn't a collection is rendered once.
            context.WriteLine("if (list == null)");
            context.WriteLine("{");
            using (context.Indent())
            {
                EmitApplyAssignStatements();
                var name = string.IsNullOrEmpty(Alias) ? "identifier" : SourceGenerationContext.ToCSharpStringLiteral(Alias);
                context.WriteLine($"{context.ContextName}.SetValue({name}, evaluatedFor);");
                context.WriteLine($"await template.RenderAsync({context.WriterName}, {context.EncoderName}, {context.ContextName});");
                context.WriteLine("return Completion.Normal;");
            }
            context.WriteLine("}");

            // Nothing reads the forloop variable of most loops, which then don't create or update it.
            var usesForLoop = context.UsesForLoop(this);

            context.WriteLine("var length = list.Count;");

            if (usesForLoop)
            {
                context.WriteLine("var forloop = ForLoopValue.Create();");
                context.WriteLine("forloop.IsRenderLoop = true;");
                context.WriteLine("forloop.Length = length;");
            }

            context.WriteLine("for (var i = 0; i < length; i++)");
            context.WriteLine("{");
            using (context.Indent())
            {
                context.WriteLine($"{context.ContextName}.IncrementSteps();");
                context.WriteLine($"using var itemScope = {context.ContextName}.EnterScope(ScopeBehavior.Isolated);");
                EmitApplyAssignStatements();

                if (usesForLoop)
                {
                    context.WriteLine($"{context.ContextName}.SetValue(\"forloop\", forloop);");
                }

                context.WriteLine("var item = list[i];");

                if (!string.IsNullOrEmpty(Alias))
                {
                    context.WriteLine($"{context.ContextName}.SetValue({SourceGenerationContext.ToCSharpStringLiteral(Alias)}, item);");
                }
                else
                {
                    context.WriteLine($"{context.ContextName}.SetValue(identifier, item);");
                }

                if (usesForLoop)
                {
                    context.WriteLine("forloop.Index = i + 1;");
                    context.WriteLine("forloop.Index0 = i;");
                    context.WriteLine("forloop.RIndex = length - i;");
                    context.WriteLine("forloop.RIndex0 = length - i - 1;");
                    context.WriteLine("forloop.First = i == 0;");
                    context.WriteLine("forloop.Last = i == length - 1;");
                }

                context.WriteLine($"await template.RenderAsync({context.WriterName}, {context.EncoderName}, {context.ContextName});");
            }
            context.WriteLine("}");
        }
        else
        {
            EmitApplyAssignStatements();
            context.WriteLine($"await template.RenderAsync({context.WriterName}, {context.EncoderName}, {context.ContextName});");
        }

        context.WriteLine("return Completion.Normal;");
    }

    /// <summary>
    /// Whether <c>render ... for</c> loops over a value: like Liquid, only arrays, ranges and hashes, which respond to <c>each</c> and <c>count</c>.
    /// </summary>
    internal static bool IsCollection(FluidValue value) => value.Type is FluidValues.Array or FluidValues.Dictionary;

    private static async ValueTask<KeyValuePair<string, FluidValue>[]> EvaluateAssignStatementsAsync(
        IReadOnlyList<AssignStatement> assignStatements,
        TemplateContext context)
    {
        var length = assignStatements.Count;
        if (length == 0)
        {
            return [];
        }

        var evaluatedValues = new KeyValuePair<string, FluidValue>[length];

        for (var i = 0; i < length; i++)
        {
            context.IncrementSteps();

            var assignStatement = assignStatements[i];
            var value = await assignStatement.Value.EvaluateAsync(context);

            if (context.Assigned != null)
            {
                value = await context.Assigned.Invoke(assignStatement.Identifier, value, context);
            }

            evaluatedValues[i] = new KeyValuePair<string, FluidValue>(assignStatement.Identifier, value);
        }

        return evaluatedValues;
    }

    private static void ApplyAssignStatements(
        KeyValuePair<string, FluidValue>[] assignedValues,
        TemplateContext context)
    {
        for (var i = 0; i < assignedValues.Length; i++)
        {
            var entry = assignedValues[i];
            context.SetValue(entry.Key, entry.Value);
        }
    }
}
