using Fluid.Ast;

namespace Fluid.SourceGeneration;

/// <summary>
/// Collects the templates referenced by <c>render</c>, <c>include</c> and <c>from</c>, which are compiled with the template.
/// </summary>
internal sealed class RenderDependencyVisitor : AstVisitor
{
    private readonly List<PartialReference> _references = [];

    public IReadOnlyList<PartialReference> References => _references;

    protected internal override Statement VisitRenderStatement(RenderStatement renderStatement)
    {
        _references.Add(new PartialReference(renderStatement.Path, renderStatement.Parser));
        return base.VisitRenderStatement(renderStatement);
    }

    protected internal override Statement VisitIncludeStatement(IncludeStatement includeStatement)
    {
        // A dynamic path can't be resolved at build time, IncludeStatement.WriteTo reports it.
        if (SourceGenerationContext.TryGetLiteralString(includeStatement.Path, out var path))
        {
            _references.Add(new PartialReference(path, includeStatement.Parser));
        }

        return base.VisitIncludeStatement(includeStatement);
    }

    protected internal override Statement VisitFromStatement(FromStatement fromStatement)
    {
        if (SourceGenerationContext.TryGetLiteralString(fromStatement.Path, out var path))
        {
            _references.Add(new PartialReference(FromStatement.GetTemplatePath(path), fromStatement.Parser));
        }

        return base.VisitFromStatement(fromStatement);
    }
}

internal sealed record PartialReference(string Path, FluidParser Parser);
