using Microsoft.Extensions.FileProviders;

namespace Fluid.SourceGeneration
{
    public sealed class SourceGenerationOptions
    {
        public string Namespace { get; set; } = "Fluid.SourceGenerated";
        public string ClassName { get; set; } = "GeneratedFluidTemplate";

        /// <summary>
        /// Optional file provider used at compile-time to load sub-templates (e.g. for the <c>render</c> tag).
        /// </summary>
        public IFileProvider FileProvider { get; set; }

        /// <summary>
        /// Optional known model type. When set, code generation can optimize model access.
        /// </summary>
        public Type ModelType { get; set; }
    }
}
