using System.Buffers;
using Fluid.Values;
using System.Text.Encodings.Web;
using Fluid.SourceGeneration;

namespace Fluid.Ast
{
    /// <summary>
    /// The render tag can only access immutable environments, which means the scope of the context that was passed to the main template, the options' scope, and the model.
    /// </summary>
#pragma warning disable CA1001 // Types that own disposable fields should be disposable
    public sealed class RenderStatement : Statement, ISourceable
#pragma warning restore CA1001
    {
        public const string ViewExtension = ".liquid";

        private readonly string _identifier;
        private CachedTemplateResolution _firstCachedTemplate;
        private CachedTemplateResolution _secondCachedTemplate;

        public RenderStatement(FluidParser parser, string path, Expression with = null, Expression @for = null, string alias = null, IReadOnlyList<AssignStatement> assignStatements = null)
        {
            Parser = parser;
            Path = path;
            _identifier = System.IO.Path.GetFileNameWithoutExtension(path);
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

            var task = LoadTemplateAsync(context);

            if (task.IsCompletedSuccessfully)
            {
                return RenderLoadedTemplate(task.Result.Template, output, encoder, context);
            }

            return AwaitedLoad(task, output, encoder, context);

            static async ValueTask<Completion> AwaitedLoad(
                ValueTask<LoadedRenderTemplate> task,
                IFluidOutput output,
                TextEncoder encoder,
                TemplateContext context)
            {
                var loadedTemplate = await task;
                return await RenderLoadedTemplate(loadedTemplate.Template, output, encoder, context);
            }
        }

        private ValueTask<LoadedRenderTemplate> LoadTemplateAsync(TemplateContext context)
        {
            var options = context.Options;
            var provider = options.FileProvider;
            var templateCache = options.TemplateCache;
            var defaultFileExtension = options.DefaultFileExtension;

            if (templateCache == null || provider is not IVersionedTemplateFileProvider versionedProvider)
            {
                return LoadUncachedTemplateAsync(context, defaultFileExtension);
            }

            context.CancellationToken.ThrowIfCancellationRequested();

            var resolutionCacheKey = versionedProvider.GetTemplateResolutionCacheKey(context);
            if (resolutionCacheKey == null)
            {
                return LoadUncachedTemplateAsync(context, defaultFileExtension);
            }

            var version = versionedProvider.Version;

            if (TryGetCachedTemplate(
                    Volatile.Read(ref _firstCachedTemplate),
                    provider,
                    templateCache,
                    defaultFileExtension,
                    resolutionCacheKey,
                    version,
                    out var loadedTemplate) ||
                TryGetCachedTemplate(
                    Volatile.Read(ref _secondCachedTemplate),
                    provider,
                    templateCache,
                    defaultFileExtension,
                    resolutionCacheKey,
                    version,
                    out loadedTemplate))
            {
                return new ValueTask<LoadedRenderTemplate>(loadedTemplate);
            }

            return LoadAndCacheTemplate(
                context,
                options,
                provider,
                versionedProvider,
                templateCache,
                defaultFileExtension,
                resolutionCacheKey,
                version);
        }

        private static bool TryGetCachedTemplate(
            CachedTemplateResolution cached,
            ITemplateFileProvider provider,
            ITemplateCache templateCache,
            string defaultFileExtension,
            object resolutionCacheKey,
            long version,
            out LoadedRenderTemplate loadedTemplate)
        {
            if (cached != null &&
                cached.Version == version &&
                cached.Matches(provider, templateCache, defaultFileExtension, resolutionCacheKey) &&
                templateCache.TryGetTemplate(cached.CacheKey, cached.LastModified, out var template))
            {
                loadedTemplate = new LoadedRenderTemplate(
                    template,
                    cached.Identifier);
                return true;
            }

            loadedTemplate = default;
            return false;
        }

        private ValueTask<LoadedRenderTemplate> LoadAndCacheTemplate(
            TemplateContext context,
            TemplateOptions options,
            ITemplateFileProvider provider,
            IVersionedTemplateFileProvider versionedProvider,
            ITemplateCache templateCache,
            string defaultFileExtension,
            object resolutionCacheKey,
            long version)
        {
            var capture = new TemplateLoader.LoadCapture();
            var task = TemplateLoader.LoadAsync(
                Parser,
                Path,
                context,
                defaultFileExtension,
                capture);

            if (task.IsCompletedSuccessfully)
            {
                return new ValueTask<LoadedRenderTemplate>(
                    CacheLoadedTemplate(
                        task.Result,
                        capture,
                        context,
                        options,
                        provider,
                        versionedProvider,
                        templateCache,
                        defaultFileExtension,
                        resolutionCacheKey,
                        version));
            }

            return AwaitedLoadAndCache(
                this,
                task,
                capture,
                context,
                options,
                provider,
                versionedProvider,
                templateCache,
                defaultFileExtension,
                resolutionCacheKey,
                version);

            static async ValueTask<LoadedRenderTemplate> AwaitedLoadAndCache(
                RenderStatement statement,
                ValueTask<TemplateLoader.LoadedTemplate> task,
                TemplateLoader.LoadCapture capture,
                TemplateContext context,
                TemplateOptions options,
                ITemplateFileProvider provider,
                IVersionedTemplateFileProvider versionedProvider,
                ITemplateCache templateCache,
                string defaultFileExtension,
                object resolutionCacheKey,
                long version)
            {
                var loadedTemplate = await task;
                return statement.CacheLoadedTemplate(
                    loadedTemplate,
                    capture,
                    context,
                    options,
                    provider,
                    versionedProvider,
                    templateCache,
                    defaultFileExtension,
                    resolutionCacheKey,
                    version);
            }
        }

        private LoadedRenderTemplate CacheLoadedTemplate(
            TemplateLoader.LoadedTemplate loadedTemplate,
            TemplateLoader.LoadCapture capture,
            TemplateContext context,
            TemplateOptions options,
            ITemplateFileProvider provider,
            IVersionedTemplateFileProvider versionedProvider,
            ITemplateCache templateCache,
            string defaultFileExtension,
            object resolutionCacheKey,
            long version)
        {
            string identifier = null;
            if (With != null || For != null)
            {
                identifier = string.Equals(capture.Path, Path, StringComparison.Ordinal)
                    ? _identifier
                    : System.IO.Path.GetFileNameWithoutExtension(capture.Path);
            }

            if (versionedProvider.Version == version &&
                ReferenceEquals(context.Options, options) &&
                ReferenceEquals(options.FileProvider, provider) &&
                ReferenceEquals(options.TemplateCache, templateCache) &&
                string.Equals(options.DefaultFileExtension, defaultFileExtension, StringComparison.Ordinal) &&
                ReferenceEquals(
                    versionedProvider.GetTemplateResolutionCacheKey(context),
                    resolutionCacheKey))
            {
                CacheTemplateResolution(
                    new CachedTemplateResolution(
                        provider,
                        templateCache,
                        defaultFileExtension,
                        resolutionCacheKey,
                        version,
                        capture.CacheKey,
                        capture.LastModified,
                        identifier));
            }

            return new LoadedRenderTemplate(loadedTemplate.Template, identifier);
        }

        private ValueTask<LoadedRenderTemplate> LoadUncachedTemplateAsync(
            TemplateContext context,
            string defaultFileExtension)
        {
            var task = TemplateLoader.LoadAsync(Parser, Path, context, defaultFileExtension);

            if (task.IsCompletedSuccessfully)
            {
                return new ValueTask<LoadedRenderTemplate>(CreateLoadedRenderTemplate(task.Result));
            }

            return AwaitedLoad(this, task);

            static async ValueTask<LoadedRenderTemplate> AwaitedLoad(
                RenderStatement statement,
                ValueTask<TemplateLoader.LoadedTemplate> task) =>
                statement.CreateLoadedRenderTemplate(await task);
        }

        private LoadedRenderTemplate CreateLoadedRenderTemplate(TemplateLoader.LoadedTemplate loadedTemplate)
        {
            string identifier = null;
            if (With != null || For != null)
            {
                identifier = string.Equals(loadedTemplate.Path, Path, StringComparison.Ordinal)
                    ? _identifier
                    : System.IO.Path.GetFileNameWithoutExtension(loadedTemplate.Path);
            }

            return new LoadedRenderTemplate(loadedTemplate.Template, identifier);
        }

        private void CacheTemplateResolution(CachedTemplateResolution cached)
        {
            var first = Volatile.Read(ref _firstCachedTemplate);
            if (first == null || first.MatchesConfiguration(cached))
            {
                Volatile.Write(ref _firstCachedTemplate, cached);
                return;
            }

            var second = Volatile.Read(ref _secondCachedTemplate);
            if (second == null || second.MatchesConfiguration(cached))
            {
                Volatile.Write(ref _secondCachedTemplate, cached);
                return;
            }

            Volatile.Write(ref _firstCachedTemplate, cached);
        }

        private static ValueTask<Completion> RenderLoadedTemplate(
            IFluidTemplate template,
            IFluidOutput output,
            TextEncoder encoder,
            TemplateContext context)
        {
            context.EnterChildScope();
            var previousScope = context.LocalScope;

            try
            {
                context.IsolateCurrentScope();

                var task = FluidTemplateRenderer.RenderAsync(template, output, encoder, context);
                if (task.IsCompletedSuccessfully)
                {
                    context.LocalScope = previousScope;
                    context.ReleaseScope();
                    return NormalCompletion;
                }

                return AwaitedRender(task, previousScope, context);
            }
            catch
            {
                context.LocalScope = previousScope;
                context.ReleaseScope();
                throw;
            }

            static async ValueTask<Completion> AwaitedRender(
                ValueTask task,
                Scope previousScope,
                TemplateContext context)
            {
                try
                {
                    await task;
                    return Completion.Normal;
                }
                finally
                {
                    context.LocalScope = previousScope;
                    context.ReleaseScope();
                }
            }
        }

        private async ValueTask<Completion> WriteToAsyncCore(IFluidOutput output, TextEncoder encoder, TemplateContext context)
        {
            context.IncrementSteps();

            var loadedTemplate = await LoadTemplateAsync(context);
            var template = loadedTemplate.Template;
            var identifier = loadedTemplate.Identifier;

            context.EnterChildScope();
            var previousScope = context.LocalScope;

            try
            {
                if (With != null)
                {
                    var with = await With.EvaluateAsync(context);

                    context.IsolateCurrentScope();

                    context.SetValue(Alias ?? identifier, with);

                    // Evaluate assign statements in the new scope if present
                    if (AssignStatements.Count > 0)
                    {
                        await EvaluateAssignStatementsAsync(AssignStatements, context);
                    }

                    await FluidTemplateRenderer.RenderAsync(template, output, encoder, context);
                }
                else if (For != null)
                {
                    try
                    {
                        var forloop = new ForLoopValue { IsRenderLoop = true };

                        var evaluatedFor = await For.EvaluateAsync(context);

                        // Fast-path: avoid re-enumerating already materialized arrays.
                        IReadOnlyList<FluidValue> list = evaluatedFor is ArrayValue array
                            ? array.Values
                            : await evaluatedFor.EnumerateAsync(context).ToListAsync(context.CancellationToken);

                        context.IsolateCurrentScope();

                        // Evaluate assign statements in the new scope before the loop if present
                        if (AssignStatements.Count > 0)
                        {
                            await EvaluateAssignStatementsAsync(AssignStatements, context);
                        }

                        var length = forloop.Length = list.Count;

                        context.SetValue("forloop", forloop);

                        for (var i = 0; i < length; i++)
                        {
                            context.IncrementSteps();

                            var item = list[i];

                            context.SetValue(Alias ?? identifier, item);

                            // Set helper variables
                            forloop.Index = i + 1;
                            forloop.Index0 = i;
                            forloop.RIndex = length - i;
                            forloop.RIndex0 = length - i - 1;
                            forloop.First = i == 0;
                            forloop.Last = i == length - 1;

                            await FluidTemplateRenderer.RenderAsync(template, output, encoder, context);

                            // Restore the forloop property after every statement in case it replaced it,
                            // for instance if it contains a nested for loop
                            context.SetValue("forloop", forloop);
                        }
                    }
                    finally
                    {
                        context.LocalScope.Delete("forloop");
                    }
                }
                else if (AssignStatements.Count > 0)
                {
                    await EvaluateAssignStatementsAsync(AssignStatements, context);

                    context.IsolateCurrentScope();

                    await FluidTemplateRenderer.RenderAsync(template, output, encoder, context);
                }
                else
                {
                    context.IsolateCurrentScope();

                    await FluidTemplateRenderer.RenderAsync(template, output, encoder, context);
                }
            }
            finally
            {
                context.LocalScope = previousScope;
                context.ReleaseScope();
            }

            return Completion.Normal;
        }

        protected internal override Statement Accept(AstVisitor visitor) => visitor.VisitRenderStatement(this);

        public void WriteTo(SourceGenerationContext context)
        {
            void EmitEvaluateAssignStatements()
            {
                var assignedValueNames = new string[AssignStatements.Count];

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

            context.WriteLine($"{context.ContextName}.EnterChildScope();");
            context.WriteLine($"var previousScope = {context.ContextName}.LocalScope;");
            context.WriteLine("try");
            context.WriteLine("{");
            using (context.Indent())
            {
                if (With != null)
                {
                    var withExpr = context.GetExpressionMethodName(With);
                    context.WriteLine($"var withValue = await {withExpr}({context.ContextName});");

                    context.WriteLine($"{context.ContextName}.IsolateCurrentScope();");

                    if (!string.IsNullOrEmpty(Alias))
                    {
                        context.WriteLine($"{context.ContextName}.SetValue({SourceGenerationContext.ToCSharpStringLiteral(Alias)}, withValue);");
                    }
                    else
                    {
                        context.WriteLine($"{context.ContextName}.SetValue({SourceGenerationContext.ToCSharpStringLiteral(_identifier)}, withValue);");
                    }

                    if (AssignStatements.Count > 0)
                    {
                        EmitEvaluateAssignStatements();
                    }

                    context.WriteLine($"await template.RenderInternalAsync({context.WriterName}, {context.EncoderName}, {context.ContextName});");
                }
                else if (For != null)
                {
                    var forExpr = context.GetExpressionMethodName(For);

                    context.WriteLine("try");
                    context.WriteLine("{");
                    using (context.Indent())
                    {
                        context.WriteLine("var forloop = new ForLoopValue { IsRenderLoop = true };");
                        context.WriteLine($"var evaluatedFor = await {forExpr}({context.ContextName});");
                        context.WriteLine("IReadOnlyList<FluidValue> list = evaluatedFor is ArrayValue array");
                        using (context.Indent())
                        {
                            context.WriteLine("? array.Values");
                            context.WriteLine($": await evaluatedFor.EnumerateAsync({context.ContextName}).ToListAsync({context.ContextName}.CancellationToken);");
                        }

                        context.WriteLine($"{context.ContextName}.IsolateCurrentScope();");

                        if (AssignStatements.Count > 0)
                        {
                            EmitEvaluateAssignStatements();
                        }

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
                                context.WriteLine($"{context.ContextName}.SetValue({SourceGenerationContext.ToCSharpStringLiteral(_identifier)}, item);");
                            }

                            context.WriteLine("forloop.Index = i + 1;");
                            context.WriteLine("forloop.Index0 = i;");
                            context.WriteLine("forloop.RIndex = length - i;");
                            context.WriteLine("forloop.RIndex0 = length - i - 1;");
                            context.WriteLine("forloop.First = i == 0;");
                            context.WriteLine("forloop.Last = i == length - 1;");

                            context.WriteLine($"await template.RenderInternalAsync({context.WriterName}, {context.EncoderName}, {context.ContextName});");
                            context.WriteLine($"{context.ContextName}.SetValue(\"forloop\", forloop);");
                        }
                        context.WriteLine("}");
                    }
                    context.WriteLine("}");
                    context.WriteLine("finally");
                    context.WriteLine("{");
                    using (context.Indent())
                    {
                        context.WriteLine($"{context.ContextName}.LocalScope.Delete(\"forloop\");");
                    }
                    context.WriteLine("}");
                }
                else if (AssignStatements.Count > 0)
                {
                    EmitEvaluateAssignStatements();

                    context.WriteLine($"{context.ContextName}.IsolateCurrentScope();");
                    context.WriteLine($"await template.RenderInternalAsync({context.WriterName}, {context.EncoderName}, {context.ContextName});");
                }
                else
                {
                    context.WriteLine($"{context.ContextName}.IsolateCurrentScope();");
                    context.WriteLine($"await template.RenderInternalAsync({context.WriterName}, {context.EncoderName}, {context.ContextName});");
                }
            }
            context.WriteLine("}");
            context.WriteLine("finally");
            context.WriteLine("{");
            using (context.Indent())
            {
                context.WriteLine($"{context.ContextName}.LocalScope = previousScope;");
                context.WriteLine($"{context.ContextName}.ReleaseScope();");
            }
            context.WriteLine("}");

            context.WriteLine("return Completion.Normal;");
        }

        private static async ValueTask EvaluateAssignStatementsAsync(IReadOnlyList<AssignStatement> assignStatements, TemplateContext context)
        {
            var length = assignStatements.Count;

            if (length == 1)
            {
                context.IncrementSteps();

                var assignStatement = assignStatements[0];
                var value = await assignStatement.Value.EvaluateAsync(context);

                if (context.Assigned != null)
                {
                    value = await context.Assigned.Invoke(assignStatement.Identifier, value, context);
                }

                context.SetValue(assignStatement.Identifier, value);
                return;
            }

            var evaluatedValues = ArrayPool<FluidValue>.Shared.Rent(length);

            try
            {
                for (var i = 0; i < length; i++)
                {
                    context.IncrementSteps();

                    var assignStatement = assignStatements[i];
                    var value = await assignStatement.Value.EvaluateAsync(context);

                    if (context.Assigned != null)
                    {
                        value = await context.Assigned.Invoke(assignStatement.Identifier, value, context);
                    }

                    evaluatedValues[i] = value;
                }

                for (var i = 0; i < length; i++)
                {
                    context.SetValue(assignStatements[i].Identifier, evaluatedValues[i]);
                }
            }
            finally
            {
                Array.Clear(evaluatedValues, 0, length);
                ArrayPool<FluidValue>.Shared.Return(evaluatedValues);
            }
        }

        private sealed record CachedTemplateResolution(
            ITemplateFileProvider Provider,
            ITemplateCache TemplateCache,
            string DefaultFileExtension,
            object ResolutionCacheKey,
            long Version,
            string CacheKey,
            DateTimeOffset LastModified,
            string Identifier)
        {
            public bool Matches(
                ITemplateFileProvider provider,
                ITemplateCache templateCache,
                string defaultFileExtension,
                object resolutionCacheKey) =>
                ReferenceEquals(Provider, provider) &&
                ReferenceEquals(TemplateCache, templateCache) &&
                string.Equals(DefaultFileExtension, defaultFileExtension, StringComparison.Ordinal) &&
                ReferenceEquals(ResolutionCacheKey, resolutionCacheKey);

            public bool MatchesConfiguration(CachedTemplateResolution other) =>
                Matches(
                    other.Provider,
                    other.TemplateCache,
                    other.DefaultFileExtension,
                    other.ResolutionCacheKey);
        }

        private readonly record struct LoadedRenderTemplate(
            IFluidTemplate Template,
            string Identifier);
    }
}
