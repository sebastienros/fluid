using System.Collections.Immutable;
using System.Linq;
using Fluid.Parser;
using Fluid.SourceGeneration;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Diagnostics;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using System.Text;

namespace Fluid.SourceGenerator
{
    [Generator]
    public sealed class FluidTemplateGenerator : IIncrementalGenerator
    {
        private const string TemplatesAttributeFullName = "Fluid.SourceGenerator.FluidTemplatesAttribute";

        public void Initialize(IncrementalGeneratorInitializationContext context)
        {
            context.RegisterPostInitializationOutput(static ctx =>
            {
                ctx.AddSource("FluidTemplatesAttribute.g.cs", AttributeSource);
            });

            var templateClasses = context.SyntaxProvider
                .CreateSyntaxProvider(
                    predicate: static (node, _) => node is ClassDeclarationSyntax c && c.AttributeLists.Count > 0,
                    transform: static (syntaxContext, _) => TryGetTemplateClassInfo(syntaxContext))
                .Where(static c => c is not null)
                .Select(static (c, _) => c!);

            var additionalFiles = context.AdditionalTextsProvider.Collect();
            var globalOptions = context.AnalyzerConfigOptionsProvider.Select(static (p, _) => p.GlobalOptions);

            var inputs = templateClasses
                .Combine(additionalFiles)
                .Combine(globalOptions);

            context.RegisterSourceOutput(inputs, static (spc, input) =>
            {
                var classSymbol = input.Left.Left.Class;
                var attribute = input.Left.Left.Attribute;
                var files = input.Left.Right;
                var options = input.Right;

                Execute(spc, classSymbol, attribute, files, options);
            });
        }

        private static TemplateClassInfo? TryGetTemplateClassInfo(GeneratorSyntaxContext context)
        {
            if (context.Node is not ClassDeclarationSyntax @class)
            {
                return null;
            }

            foreach (var list in @class.AttributeLists)
            {
                foreach (var attribute in list.Attributes)
                {
                    var symbol = context.SemanticModel.GetSymbolInfo(attribute).Symbol;
                    if (symbol is not IMethodSymbol attributeCtor)
                    {
                        continue;
                    }

                    var attributeType = attributeCtor.ContainingType;
                    var fullName = attributeType.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat)
                        .Replace("global::", string.Empty);

                    if (fullName == TemplatesAttributeFullName)
                    {
                        if (context.SemanticModel.GetDeclaredSymbol(@class) is not INamedTypeSymbol classSymbol)
                        {
                            return null;
                        }

                        var attrData = classSymbol.GetAttributes().FirstOrDefault(a =>
                            string.Equals(
                                a.AttributeClass?.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat).Replace("global::", string.Empty),
                                TemplatesAttributeFullName,
                                System.StringComparison.Ordinal));

                        if (attrData == null)
                        {
                            return null;
                        }

                        return new TemplateClassInfo(classSymbol, attrData, @class.GetLocation());
                    }
                }
            }

            return null;
        }

        private static void Execute(
            SourceProductionContext context,
            INamedTypeSymbol classSymbol,
            AttributeData templatesAttribute,
            ImmutableArray<AdditionalText> additionalFiles,
            AnalyzerConfigOptions options)
        {
            var location = classSymbol.Locations.FirstOrDefault() ?? Location.None;

            if (classSymbol.TypeKind != TypeKind.Class)
            {
                return;
            }

            // Must be partial so we can add generated members.
            var hasPartialModifier = classSymbol.DeclaringSyntaxReferences
                .Select(r => r.GetSyntax())
                .OfType<ClassDeclarationSyntax>()
                .Any(s => s.Modifiers.Any(m => m.IsKind(SyntaxKind.PartialKeyword)));

            if (!hasPartialModifier)
            {
                context.ReportDiagnostic(Diagnostics.ClassMustBePartial(location));
                return;
            }

            var projectOptions = new ProjectOptions(options);

            var includePatterns = AttributeReader.GetConstructorStringArray(templatesAttribute) ?? System.Array.Empty<string>();
            var excludePatterns = AttributeReader.GetNamedStringArray(templatesAttribute, "Exclude") ?? System.Array.Empty<string>();
            var generatedNamespaceOverride = AttributeReader.GetNamedString(templatesAttribute, "Namespace");

            var templates = AdditionalFilesTemplateProvider.GetTemplates(additionalFiles, projectOptions);
            var matches = templates
                .Where(t => Globber.IsMatchAny(includePatterns, t.Path) && !Globber.IsMatchAny(excludePatterns, t.Path))
                .ToList();

            if (matches.Count == 0)
            {
                // Nothing to generate.
                return;
            }

            var fileProvider = AdditionalFilesTemplateProvider.BuildFileProvider(templates);
            var parser = new FluidParser();

            // Compiled templates are emitted into a separate namespace to avoid polluting the user type.
            var compiledNamespace = generatedNamespaceOverride ?? (classSymbol.ContainingNamespace?.ToDisplayString() ?? "") + ".FluidTemplates";
            if (compiledNamespace.StartsWith(".", System.StringComparison.Ordinal))
            {
                compiledNamespace = "FluidTemplates";
            }

            var propertyMap = new Dictionary<string, string>(System.StringComparer.Ordinal);
            var usedPropertyNames = new HashSet<string>(System.StringComparer.Ordinal);

            foreach (var tpl in matches)
            {
                if (!parser.TryParse(tpl.Content, out var parsedTemplate, out var parseErrors))
                {
                    context.ReportDiagnostic(Diagnostics.TemplateParseFailed(location, tpl.Path, string.Join("; ", parseErrors)));
                    continue;
                }

                var propertyName = NameHelper.GetPropertyName(tpl.Path);
                propertyName = NameHelper.EnsureUnique(propertyName, usedPropertyNames);
                usedPropertyNames.Add(propertyName);

                var className = NameHelper.SanitizeIdentifier(classSymbol.Name + "_" + propertyName + "_Template");

                var sourceOptions = new SourceGenerationOptions
                {
                    Namespace = compiledNamespace,
                    ClassName = className,
                    FileProvider = fileProvider
                };

                var compiled = TemplateSourceGenerator.Generate(parsedTemplate, sourceOptions);
                context.AddSource($"{classSymbol.Name}.{propertyName}.FluidTemplate.g.cs", compiled.SourceCode);
                propertyMap[propertyName] = "global::" + compiled.FullTypeName;
            }

            if (propertyMap.Count == 0)
            {
                return;
            }

            var propertiesSource = PropertiesEmitter.Emit(classSymbol, propertyMap);
            context.AddSource($"{classSymbol.Name}.FluidTemplates.Properties.g.cs", propertiesSource);
        }

        private static readonly string AttributeSource = @"// <auto-generated />
#nullable enable
using System;

namespace Fluid.SourceGenerator
{
    [AttributeUsage(AttributeTargets.Class, AllowMultiple = false, Inherited = false)]
    internal sealed class FluidTemplatesAttribute : Attribute
    {
        public FluidTemplatesAttribute(params string[] include)
        {
            Include = include ?? Array.Empty<string>();
        }

        public string[] Include { get; }

        public string[]? Exclude { get; set; }

        /// <summary>
        /// Optional namespace for the compiled template types.
        /// </summary>
        public string? Namespace { get; set; }
    }
}
";

        private sealed record TemplateClassInfo(INamedTypeSymbol Class, AttributeData Attribute, Location Location);
    }
}
