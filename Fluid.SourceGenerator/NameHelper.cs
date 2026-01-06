using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Text;

namespace Fluid.SourceGenerator
{
    internal static class NameHelper
    {
        public static string GetPropertyName(string templatePath)
        {
            var fileName = Path.GetFileNameWithoutExtension(templatePath) ?? "Template";
            return ToPascalCaseIdentifier(fileName);
        }

        public static string EnsureUnique(string name, HashSet<string> used)
        {
            if (!used.Contains(name))
            {
                return name;
            }

            var i = 2;
            while (used.Contains(name + i.ToString(CultureInfo.InvariantCulture)))
            {
                i++;
            }

            return name + i.ToString(CultureInfo.InvariantCulture);
        }

        public static string SanitizeIdentifier(string name)
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

            if (!char.IsLetter(sb[0]) && sb[0] != '_')
            {
                sb.Insert(0, '_');
            }

            return sb.ToString();
        }

        private static string ToPascalCaseIdentifier(string value)
        {
            var sb = new StringBuilder(value.Length);
            var upperNext = true;

            for (var i = 0; i < value.Length; i++)
            {
                var c = value[i];

                if (!char.IsLetterOrDigit(c))
                {
                    upperNext = true;
                    continue;
                }

                if (upperNext)
                {
                    sb.Append(char.ToUpperInvariant(c));
                    upperNext = false;
                }
                else
                {
                    sb.Append(c);
                }
            }

            var result = sb.Length == 0 ? "Template" : sb.ToString();
            if (!char.IsLetter(result[0]) && result[0] != '_')
            {
                result = "T" + result;
            }

            return result;
        }
    }
}
