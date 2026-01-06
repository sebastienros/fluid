using Microsoft.CodeAnalysis;

namespace Fluid.SourceGenerator
{
    internal enum TemplateRequestKind
    {
        File,
        Inline
    }

    internal sealed record TemplateRequest(
        TemplateRequestKind Kind,
        string Value,
        string? Namespace,
        string? ClassName)
    {
        public static TemplateRequest? TryCreate(IMethodSymbol methodSymbol)
        {
            foreach (var attr in methodSymbol.GetAttributes())
            {
                var name = attr.AttributeClass?.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat)
                    .Replace("global::", string.Empty);

                if (name == "Fluid.SourceGenerator.FluidTemplateAttribute")
                {
                    if (attr.ConstructorArguments.Length == 1 && attr.ConstructorArguments[0].Value is string file)
                    {
                        return new TemplateRequest(
                            TemplateRequestKind.File,
                            file,
                            Namespace: GetNamedString(attr, "Namespace"),
                            ClassName: GetNamedString(attr, "ClassName"));
                    }
                }

                if (name == "Fluid.SourceGenerator.FluidInlineTemplateAttribute")
                {
                    if (attr.ConstructorArguments.Length == 1 && attr.ConstructorArguments[0].Value is string template)
                    {
                        return new TemplateRequest(
                            TemplateRequestKind.Inline,
                            template,
                            Namespace: GetNamedString(attr, "Namespace"),
                            ClassName: GetNamedString(attr, "ClassName"));
                    }
                }
            }

            return null;
        }

        private static string? GetNamedString(AttributeData data, string name)
        {
            foreach (var kv in data.NamedArguments)
            {
                if (kv.Key == name && kv.Value.Value is string s)
                {
                    return s;
                }
            }

            return null;
        }
    }
}
