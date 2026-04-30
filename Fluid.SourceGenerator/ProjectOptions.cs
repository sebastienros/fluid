using Microsoft.CodeAnalysis.Diagnostics;

namespace Fluid.SourceGenerator
{
    internal sealed class ProjectOptions
    {
        public ProjectOptions(AnalyzerConfigOptions globalOptions)
        {
            globalOptions.TryGetValue("build_property.ProjectDir", out var projectDir);
            ProjectDir = projectDir;

            globalOptions.TryGetValue("build_property.FluidTemplatesFolder", out var templatesFolder);
            TemplatesFolder = string.IsNullOrWhiteSpace(templatesFolder) ? null : templatesFolder;
        }

        public string? ProjectDir { get; }
        public string? TemplatesFolder { get; }

        public string CombineTemplatePath(string fileName)
        {
            if (string.IsNullOrEmpty(TemplatesFolder))
            {
                return Normalize(fileName);
            }

            return Normalize(TemplatesFolder + "/" + fileName);
        }

        private static string Normalize(string path) => path.Replace('\\', '/').TrimStart('/');
    }
}
