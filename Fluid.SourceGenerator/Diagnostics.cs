using Microsoft.CodeAnalysis;

namespace Fluid.SourceGenerator
{
    internal static class Diagnostics
    {
        private static readonly DiagnosticDescriptor ClassMustBePartialRule = new(
            id: "FLUIDSG101",
            title: "Fluid templates class must be partial",
            messageFormat: "Class must be declared 'partial'",
            category: "Fluid.SourceGenerator",
            DiagnosticSeverity.Error,
            isEnabledByDefault: true);

        private static readonly DiagnosticDescriptor TemplateParseFailedRule = new(
            id: "FLUIDSG105",
            title: "Fluid template parse failed",
            messageFormat: "Template '{0}' could not be parsed: {1}",
            category: "Fluid.SourceGenerator",
            DiagnosticSeverity.Error,
            isEnabledByDefault: true);

        public static Diagnostic ClassMustBePartial(Location location) => Diagnostic.Create(ClassMustBePartialRule, location);
        public static Diagnostic TemplateParseFailed(Location location, string template, string errors) => Diagnostic.Create(TemplateParseFailedRule, location, template, errors);
    }
}
