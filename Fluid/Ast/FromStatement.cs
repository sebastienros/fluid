using System.ComponentModel;
using System.Text.Encodings.Web;
using Fluid.SourceGeneration;
using Fluid.Values;
using Fluid.Utils;

namespace Fluid.Ast;

#pragma warning disable CA1001 // Types that own disposable fields should be disposable
public sealed class FromStatement : Statement, ISourceable
#pragma warning restore CA1001
{
    public const string ViewExtension = ".liquid";

    public FromStatement(FluidParser parser, Expression path, IReadOnlyList<string> functions = null)
    {
        Parser = parser;
        Path = path;
        Functions = functions ?? [];
    }

    public FluidParser Parser { get; }
    public Expression Path { get; }
    public IReadOnlyList<string> Functions { get; }

    public override bool IsWhitespaceOrCommentOnly => true;

    public override async ValueTask<Completion> WriteToAsync(IFluidOutput output, TextEncoder encoder, TemplateContext context)
    {
        var relativePath = GetTemplatePath((await Path.EvaluateAsync(context)).ToStringValue());

        var loadedTemplate = await TemplateLoader.LoadAsync(Parser, relativePath, context, defaultFileExtension: null);

        await ImportFunctionsAsync(loadedTemplate.Template, encoder, context, Functions);

        return Completion.Normal;
    }

    /// <summary>
    /// Gets the path of the template to import, which always has the <c>.liquid</c> extension.
    /// </summary>
    public static string GetTemplatePath(string path)
    {
        return path.EndsWith(ViewExtension, StringComparison.OrdinalIgnoreCase) ? path : path + ViewExtension;
    }

    /// <summary>
    /// Renders a template to collect the macros it defines, and copies them in the current scope.
    /// </summary>
    /// <param name="template">The template defining the macros.</param>
    /// <param name="encoder">The encoder.</param>
    /// <param name="context">The context.</param>
    /// <param name="functions">The names of the macros to import, or an empty list to import all of them.</param>
    [EditorBrowsable(EditorBrowsableState.Never)]
    public static async ValueTask ImportFunctionsAsync(IFluidTemplate template, TextEncoder encoder, TemplateContext context, IReadOnlyList<string> functions)
    {
        var parentScope = context.LocalScope;

        // Create a dedicated scope so we can list all macros defined in this template
        using var scope = context.EnterScope(ScopeBehavior.Local);

        await template.RenderAsync(NullFluidOutput.Instance, encoder, context);

        if (functions.Count > 0)
        {
            foreach (var functionName in functions)
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
            var values = context.LocalScope.GetOwnValues();
            while (values.MoveNext())
            {
                var property = values.Current;
                if (property.Value is FunctionValue)
                {
                    parentScope.SetValue(property.Key, property.Value);
                }
            }
        }
    }

    public void WriteTo(SourceGenerationContext context)
    {
        if (!SourceGenerationContext.TryGetLiteralString(Path, out var path))
        {
            throw new SourceGenerationException("A 'from' tag with a dynamic path is not compatible with source generation.");
        }

        // The imported template is compiled ahead-of-time.
        var templateTypeName = context.GetRenderTemplateTypeName(GetTemplatePath(path));

        var functions = Functions.Count == 0
            ? "Array.Empty<string>()"
            : "new string[] { " + string.Join(", ", Functions.Select(SourceGenerationContext.ToCSharpStringLiteral)) + " }";

        context.WriteLine($"await FromStatement.ImportFunctionsAsync(new {templateTypeName}(), {context.EncoderName}, {context.ContextName}, {functions});");
        context.WriteLine("return Completion.Normal;");
    }

    protected internal override Statement Accept(AstVisitor visitor) => visitor.VisitFromStatement(this);
}
