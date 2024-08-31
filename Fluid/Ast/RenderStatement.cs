using Fluid.Values;
using System.Text.Encodings.Web;

namespace Fluid.Ast
{
    /// <summary>
    /// The render tag can only access immutable environments, which means the scope of the context that was passed to the main template, the options' scope, and the model.
    /// </summary>
#pragma warning disable CA1001 // Types that own disposable fields should be disposable
    public sealed class RenderStatement : Statement
#pragma warning restore CA1001
    {
        public const string ViewExtension = ".liquid";
        private volatile CachedTemplate _cachedTemplate;
        private readonly SemaphoreSlim _semaphore = new SemaphoreSlim(1);

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

        public override async ValueTask<Completion> WriteToAsync(TextWriter writer, TextEncoder encoder, TemplateContext context)
        {
            context.IncrementSteps();

            var relativePath = Path;

            if (!relativePath.EndsWith(ViewExtension, StringComparison.OrdinalIgnoreCase))
            {
                relativePath += ViewExtension;
            }

            if (_cachedTemplate == null || !string.Equals(_cachedTemplate.Name, System.IO.Path.GetFileNameWithoutExtension(relativePath), StringComparison.Ordinal))
            {
                await _semaphore.WaitAsync();

                try
                {
                    if (_cachedTemplate == null || !string.Equals(_cachedTemplate.Name, System.IO.Path.GetFileNameWithoutExtension(relativePath), StringComparison.Ordinal))
                    {
                        var fileProvider = context.Options.FileProvider;

                        var fileInfo = fileProvider.GetFileInfo(relativePath);

                        if (fileInfo == null || !fileInfo.Exists)
                        {
                            throw new FileNotFoundException(relativePath);
                        }

                        var content = "";

                        using (var stream = fileInfo.CreateReadStream())
                        using (var streamReader = new StreamReader(stream))
                        {
                            content = await streamReader.ReadToEndAsync();
                        }

                        if (!Parser.TryParse(content, out var template, out var errors))
                        {
                            throw new ParseException(errors);
                        }

                        var identifier = System.IO.Path.GetFileNameWithoutExtension(relativePath);

                        _cachedTemplate = new CachedTemplate(template, identifier);
                    }
                }
                finally
                {
                    _semaphore.Release();
                }
            }

            context.EnterChildScope();
            var previousScope = context.LocalScope;

            try
            {
                if (With != null)
                {
                    var with = await With.EvaluateAsync(context);

                    context.LocalScope = new Scope(context.RootScope);
                    previousScope.CopyTo(context.LocalScope);

                    context.SetValue(Alias ?? _cachedTemplate.Name, with);
                    await _cachedTemplate.Template.RenderAsync(writer, encoder, context);
                }
                else if (AssignStatements.Count > 0)
                {
                    var length = AssignStatements.Count;
                    for (var i = 0; i < length; i++)
                    {
                        await AssignStatements[i].WriteToAsync(writer, encoder, context);
                    }

                    context.LocalScope = new Scope(context.RootScope);
                    previousScope.CopyTo(context.LocalScope);

                    await _cachedTemplate.Template.RenderAsync(writer, encoder, context);
                }
                else if (For != null)
                {
                    try
                    {
                        var forloop = new ForLoopValue();

                        var list = (await For.EvaluateAsync(context)).Enumerate(context).ToList();

                        context.LocalScope = new Scope(context.RootScope);
                        previousScope.CopyTo(context.LocalScope);

                        var length = forloop.Length = list.Count;

                        context.SetValue("forloop", forloop);

                        for (var i = 0; i < length; i++)
                        {
                            context.IncrementSteps();

                            var item = list[i];

                            context.SetValue(Alias ?? _cachedTemplate.Name, item);

                            // Set helper variables
                            forloop.Index = i + 1;
                            forloop.Index0 = i;
                            forloop.RIndex = length - i;
                            forloop.RIndex0 = length - i - 1;
                            forloop.First = i == 0;
                            forloop.Last = i == length - 1;

                            await _cachedTemplate.Template.RenderAsync(writer, encoder, context);

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
                else
                {
                    context.LocalScope = new Scope(context.RootScope);
                    previousScope.CopyTo(context.LocalScope);

                    await _cachedTemplate.Template.RenderAsync(writer, encoder, context);
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

        private sealed record CachedTemplate(IFluidTemplate Template, string Name);
    }
}
