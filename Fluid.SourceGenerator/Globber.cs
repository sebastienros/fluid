using System;
using System.Collections.Generic;
using System.Text;
using System.Text.RegularExpressions;

namespace Fluid.SourceGenerator
{
    internal static class Globber
    {
        public static bool IsMatchAny(IReadOnlyList<string> patterns, string path)
        {
            if (patterns == null || patterns.Count == 0)
            {
                return false;
            }

            for (var i = 0; i < patterns.Count; i++)
            {
                if (IsMatch(patterns[i], path))
                {
                    return true;
                }
            }

            return false;
        }

        public static bool IsMatch(string? pattern, string path)
        {
            if (string.IsNullOrWhiteSpace(pattern))
            {
                return false;
            }

            var normalizedPattern = Normalize(pattern);
            var normalizedPath = Normalize(path);

            var regex = "^" + ToRegex(normalizedPattern) + "$";
            return Regex.IsMatch(normalizedPath, regex, RegexOptions.CultureInvariant | RegexOptions.IgnoreCase);
        }

        private static string Normalize(string? s) => (s ?? string.Empty).Replace('\\', '/').TrimStart('/');

        private static string ToRegex(string pattern)
        {
            var sb = new StringBuilder(pattern.Length * 2);

            for (var i = 0; i < pattern.Length; i++)
            {
                var c = pattern[i];

                if (c == '*')
                {
                    var isDoubleStar = i + 1 < pattern.Length && pattern[i + 1] == '*';
                    if (isDoubleStar)
                    {
                        // Special-case "**/" to mean "zero or more directories".
                        if (i + 2 < pattern.Length && pattern[i + 2] == '/')
                        {
                            sb.Append("(?:.*/)?");
                            i += 2;
                        }
                        else
                        {
                            sb.Append(".*");
                            i++;
                        }
                    }
                    else
                    {
                        sb.Append("[^/]*");
                    }

                    continue;
                }

                if (c == '?')
                {
                    sb.Append("[^/]");
                    continue;
                }

                sb.Append(Regex.Escape(c.ToString()));
            }

            return sb.ToString();
        }
    }
}
