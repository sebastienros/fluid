using Microsoft.Extensions.FileProviders;
using System;
using System.Collections.Generic;
using System.Text.Encodings.Web;
using System.Threading.Tasks;

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
        public IFileProvider PartialsFileProvider { get; set; }

        /// <summary>
        /// Gets the list of view location format strings. The formatting arguments can differ for each implementation of <see cref="IFluidViewRenderer"/>.
        /// </summary>
        /// <example>
        /// /Views/{0}.liquid
        /// </example>
        public List<string> ViewsLocationFormats { get; } = new();

        /// <summary>
        /// Gets the list of partial views location format strings. The formatting arguments can differ for each implementation of <see cref="IFluidViewRenderer"/>.
        /// </summary>
        /// <example>
        /// /Views/Includes/{0}.liquid
        /// </example>
        public List<string> PartialsLocationFormats { get; } = new();

        /// <summary>
        /// Gets the list of layout location format strings. The formatting arguments can differ for each implementation of <see cref="IFluidViewRenderer"/>.
        /// </summary>
        /// <example>
        /// /Views/Shared/{0}.liquid
        /// </example>
        public List<string> LayoutsLocationFormats { get; } = new();

        /// <summary>
        /// Gets or sets whether files should be reloaded automatically when changed. Default is <code>true</code>;
        /// </summary>
        public bool TrackFileChanges { get; set; } = true;

        /// <summary>
        /// <para>Represents the method that will handle the view rendering event.</para>
        /// </summary>
        public delegate ValueTask RenderingViewDelegate(string path, TemplateContext context);

        /// <summary>
        /// Gets or sets the delegate to execute when a view is rendered.
        /// </summary>
        public RenderingViewDelegate RenderingViewAsync { get; set; }
    }
}
