using Microsoft.Extensions.FileProviders;
using System.Text.Encodings.Web;

namespace Fluid.ViewEngine
{
    public class FluidViewEngineOptions
    {
        /// <summary>
        /// Gets or set the <see cref="FluidViewParser"/> instance to use with the view engine.
        /// </summary>
        /// <remarks>
        /// To add custom tags which require special primitive elements, create a sub-class of <see cref="FluidViewParser"/>.
        /// </remarks>
        public FluidViewParser Parser { get; set; } = new FluidViewParser();

        /// <summary>
        /// Gets or sets the text encoder to use during rendering.
        /// </summary>
        public TextEncoder TextEncoder = HtmlEncoder.Default;

        /// <summary>
        /// Gets the template options.
        /// </summary>
        public TemplateOptions TemplateOptions { get; } = new TemplateOptions();

        /// <summary>
        /// Gets or sets the <see cref="IFileProvider"/> used to access views.
        /// </summary>
        public IFileProvider ViewsFileProvider { get; set; }

        /// <summary>
        /// Gets or sets the <see cref="IFileProvider"/> used to access includes.
        /// </summary>
        public IFileProvider IncludesFileProvider { get; set; }

        /// <summary>
        /// Gets or sets the path of the views. Default is <code>"Views"</code>
        /// </summary>
        public string ViewsPath { get; set; } = "Views";

    }
}
