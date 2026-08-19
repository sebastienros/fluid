using Fluid.Values;
using System.Text.Encodings.Web;
using Fluid.SourceGeneration;

namespace Fluid.Ast
{
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
            IReadOnlyList<FluidValue> list = null;

            if (With != null)
            {
                withValue = await With.EvaluateAsync(context);
            }
            else if (For != null)
            {
                var evaluatedFor = await For.EvaluateAsync(context);
                list = evaluatedFor is ArrayValue array
                    ? array.Values
                    : await evaluatedFor.EnumerateAsync(context).ToListAsync(context.CancellationToken);
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
            else if (For != null)
            {
                ApplyAssignStatements(assignedValues, context);
                var forloop = new ForLoopValue { IsRenderLoop = true };
                var length = forloop.Length = list.Count;

                context.SetValue("forloop", forloop);

                for (var i = 0; i < length; i++)
                {
                    context.IncrementSteps();

                    context.SetValue(Alias ?? identifier, list[i]);

                    forloop.Index = i + 1;
                    forloop.Index0 = i;
                    forloop.RIndex = length - i;
                    forloop.RIndex0 = length - i - 1;
                    forloop.First = i == 0;
                    forloop.Last = i == length - 1;

                    await template.RenderAsync(output, encoder, context);

                    // A nested loop can replace this name.
                    context.SetValue("forloop", forloop);
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
                context.WriteLine("IReadOnlyList<FluidValue> list = evaluatedFor is ArrayValue array");
                using (context.Indent())
                {
                    context.WriteLine("? array.Values");
                    context.WriteLine($": await evaluatedFor.EnumerateAsync({context.ContextName}).ToListAsync({context.ContextName}.CancellationToken);");
                }
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
                EmitApplyAssignStatements();
                context.WriteLine("var forloop = new ForLoopValue { IsRenderLoop = true };");
                context.WriteLine("var length = forloop.Length = list.Count;");
                context.WriteLine($"{context.ContextName}.SetValue(\"forloop\", forloop);");

                context.WriteLine("for (var i = 0; i < length; i++)");
                context.WriteLine("{");
                using (context.Indent())
                {
                    context.WriteLine($"{context.ContextName}.IncrementSteps();");
                    context.WriteLine("var item = list[i];");

                    if (!string.IsNullOrEmpty(Alias))
                    {
                        context.WriteLine($"{context.ContextName}.SetValue({SourceGenerationContext.ToCSharpStringLiteral(Alias)}, item);");
                    }
                    else
                    {
                        context.WriteLine($"{context.ContextName}.SetValue(identifier, item);");
                    }

                    context.WriteLine("forloop.Index = i + 1;");
                    context.WriteLine("forloop.Index0 = i;");
                    context.WriteLine("forloop.RIndex = length - i;");
                    context.WriteLine("forloop.RIndex0 = length - i - 1;");
                    context.WriteLine("forloop.First = i == 0;");
                    context.WriteLine("forloop.Last = i == length - 1;");
                    context.WriteLine($"await template.RenderAsync({context.WriterName}, {context.EncoderName}, {context.ContextName});");
                    context.WriteLine($"{context.ContextName}.SetValue(\"forloop\", forloop);");
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

        private sealed record CachedTemplate(IFluidTemplate Template, string Name);
    }
}
