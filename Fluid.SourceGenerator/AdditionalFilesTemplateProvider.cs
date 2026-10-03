using System.Collections.Generic;
using System.IO;
using System.Collections.Immutable;
using Fluid.SourceGeneration;
using Microsoft.CodeAnalysis;

namespace Fluid.SourceGenerator
{
    internal static class AdditionalFilesTemplateProvider
    {
        public sealed record Template(string Path, string Content);

        public static List<Template> GetTemplates(ImmutableArray<AdditionalText> additionalFiles, ProjectOptions projectOptions)
        {
            var templates = new List<Template>();

            foreach (var file in additionalFiles)
            {
                var rel = TryGetRelativePath(file.Path, projectOptions.ProjectDir);
                rel = Normalize(rel ?? Path.GetFileName(file.Path));

                // If a templates folder is configured, only include files under it and strip the prefix.
                if (!string.IsNullOrEmpty(projectOptions.TemplatesFolder))
                {
                    var folderPrefix = Normalize(projectOptions.TemplatesFolder) + "/";
                    if (!rel.StartsWith(folderPrefix, StringComparison.OrdinalIgnoreCase))
                    {
                        continue;
                    }

                    rel = rel.Substring(folderPrefix.Length);
                }

                var text = file.GetText()?.ToString();
                if (text is null)
                {
                    continue;
                }

                templates.Add(new Template(rel, text));
            }

            return templates;
        }

        public static Func<string, string> BuildTemplateResolver(IReadOnlyList<Template> templates)
        {
            var map = new Dictionary<string, string>(System.StringComparer.OrdinalIgnoreCase);

            for (var i = 0; i < templates.Count; i++)
            {
                var rel = Normalize(templates[i].Path);
                var text = templates[i].Content;

                map[rel] = text;

                // Convenience: also register without extension when file ends with .liquid
                if (rel.EndsWith(".liquid", System.StringComparison.OrdinalIgnoreCase))
                {
                    var withoutExt = rel.Substring(0, rel.Length - ".liquid".Length);
                    if (!map.ContainsKey(withoutExt))
                    {
                        map[withoutExt] = text;
                    }
                }
            }

            return path =>
            {
                if (map.TryGetValue(Normalize(path), out var content))
                {
                    return content;
                }

                throw new SourceGenerationException($"Rendered template '{path}' was not found in the provided additional files.");
            };
        }

        private static string? TryGetRelativePath(string fullPath, string? projectDir)
        {
            var dir = projectDir;
            if (dir == null || dir.Length == 0)
            {
                return null;
            }

            try
            {
                dir = dir.Replace('\\', '/');
                fullPath = fullPath.Replace('\\', '/');

                if (!dir.EndsWith("/", System.StringComparison.Ordinal))
                {
                    dir += "/";
                }

                if (fullPath.StartsWith(dir, System.StringComparison.OrdinalIgnoreCase))
                {
                    return fullPath.Substring(dir.Length);
                }
            }
            catch
            {
            }

            return null;
        }

        private static string Normalize(string? path) => (path ?? string.Empty).Replace('\\', '/').TrimStart('/');

    }
}
