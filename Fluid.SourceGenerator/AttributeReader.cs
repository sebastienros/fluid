using System;
using Microsoft.CodeAnalysis;

namespace Fluid.SourceGenerator
{
    internal static class AttributeReader
    {
        public static string[]? GetConstructorStringArray(AttributeData attribute)
        {
            if (attribute.ConstructorArguments.Length == 1)
            {
                var arg = attribute.ConstructorArguments[0];
                if (arg.Kind == TypedConstantKind.Array && !arg.IsNull)
                {
                    var values = new string[arg.Values.Length];
                    for (var i = 0; i < arg.Values.Length; i++)
                    {
                        values[i] = arg.Values[i].Value as string ?? string.Empty;
                    }

                    return values;
                }
            }

            // Also supports attributes declared as [FluidTemplates("a", "b")]
            if (attribute.ConstructorArguments.Length > 0)
            {
                var values = new string[attribute.ConstructorArguments.Length];
                for (var i = 0; i < attribute.ConstructorArguments.Length; i++)
                {
                    values[i] = attribute.ConstructorArguments[i].Value as string ?? string.Empty;
                }

                return values;
            }

            return null;
        }

        public static string? GetNamedString(AttributeData attribute, string name)
        {
            foreach (var kv in attribute.NamedArguments)
            {
                if (string.Equals(kv.Key, name, StringComparison.Ordinal) && kv.Value.Value is string s)
                {
                    return s;
                }
            }

            return null;
        }

        public static string[]? GetNamedStringArray(AttributeData attribute, string name)
        {
            foreach (var kv in attribute.NamedArguments)
            {
                if (!string.Equals(kv.Key, name, StringComparison.Ordinal))
                {
                    continue;
                }

                var value = kv.Value;
                if (value.Kind == TypedConstantKind.Array && !value.IsNull)
                {
                    var result = new string[value.Values.Length];
                    for (var i = 0; i < value.Values.Length; i++)
                    {
                        result[i] = value.Values[i].Value as string ?? string.Empty;
                    }

                    return result;
                }
            }

            return null;
        }
    }
}
