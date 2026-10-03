using System.Text;
using Microsoft.CodeAnalysis;

namespace Fluid.SourceGenerator
{
    internal static class GeneratorNames
    {
        public static string GetDefaultNamespace(IMethodSymbol method)
        {
            var ns = method.ContainingNamespace?.ToDisplayString();
            if (string.IsNullOrEmpty(ns))
            {
                return "Fluid.SourceGenerated";
            }

            return ns + ".FluidTemplates";
        }

        public static string GetDefaultClassName(IMethodSymbol method)
        {
            var typeName = method.ContainingType.Name;
            var methodName = method.Name;
            var sb = new StringBuilder();
            sb.Append(typeName).Append('_').Append(methodName).Append("_Template");
            return SanitizeIdentifier(sb.ToString());
        }

        private static string SanitizeIdentifier(string name)
        {
            if (string.IsNullOrEmpty(name))
            {
                return "Template";
            }

            var sb = new StringBuilder(name.Length);
            for (var i = 0; i < name.Length; i++)
            {
                var c = name[i];
                sb.Append(char.IsLetterOrDigit(c) || c == '_' ? c : '_');
            }

            return sb.ToString();
        }
    }
}
