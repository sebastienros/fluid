using System.Text.Encodings.Web;
using Fluid.Values;
using Fluid.Utils;

namespace Fluid.Ast
{
#pragma warning disable CA1001 // Types that own disposable fields should be disposable
    public sealed class FromStatement : Statement
#pragma warning restore CA1001
    {
        public const string ViewExtension = ".liquid";

        private volatile CachedTemplate _cachedTemplate;

        public FromStatement(FluidParser parser, Expression path, IReadOnlyList<string> functions = null)
        {
            Parser = parser;
            Path = path;
            Functions = functions ?? [];
        }

        public FluidParser Parser { get; }
        public Expression Path { get; }
        public IReadOnlyList<string> Functions { get; }

        public override async ValueTask<Completion> WriteToAsync(IFluidOutput output, TextEncoder encoder, TemplateContext context)
        {
            var relativePath = (await Path.EvaluateAsync(context)).ToStringValue();
            if (!relativePath.EndsWith(ViewExtension, StringComparison.OrdinalIgnoreCase))
            {
                relativePath += ViewExtension;
            }

            var cachedTemplate = _cachedTemplate;

            if (cachedTemplate == null || !string.Equals(cachedTemplate.Name, System.IO.Path.GetFileNameWithoutExtension(relativePath), StringComparison.Ordinal))
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
                _cachedTemplate = cachedTemplate = new CachedTemplate(template, identifier);
            }

            var parentScope = context.LocalScope;

            // Create a dedicated scope so we can list all macros defined in this template
            context.EnterChildScope();

            try
            {
                await cachedTemplate.Template.RenderAsync(NullFluidOutput.Instance, encoder, context);

                if (Functions.Count > 0)
                {
                    foreach (var functionName in Functions)
                    {
                        var value = context.LocalScope.GetValue(functionName);
                        if (value is FunctionValue)
                        {
                            parentScope.SetValue(functionName, value);
                        }
                    }
                }
                else
                {
                    foreach (var property in context.LocalScope.Properties)
                    {
                        var value = context.LocalScope.GetValue(property);
                        if (value is FunctionValue)
                        {
                            parentScope.SetValue(property, value);
                        }
                    }
                }
            }
            finally
            {
                context.ReleaseScope();
            }

            return Completion.Normal;
        }

        protected internal override Statement Accept(AstVisitor visitor) => visitor.VisitFromStatement(this);

        private sealed record CachedTemplate(IFluidTemplate Template, string Name);
    }
}
