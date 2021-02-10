using Microsoft.Extensions.FileProviders;
using System.Collections.Generic;
using System.Text.Encodings.Web;

namespace Fluid.MvcViewEngine
{
    public class FluidViewEngineOptions
    {
        /// <summary>
        /// Gets les list of view location formats.
        /// </summary>
        public IList<string> ViewLocationFormats { get; } = new List<string>();

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
        /// <remarks>
        /// If not set, the ContentRootFileProvider will be used.
        /// </remarks>
        public IFileProvider ViewsFileProvider { get; set; }

        /// <summary>
        /// Gets or sets the <see cref="IFileProvider"/> used to access includes.
        /// </summary>
        /// <remarks>
        /// If not set, the ContentRootFileProvider will be used.
        /// </remarks>
        public IFileProvider IncludesFileProvider { get; set; }
    }
}
