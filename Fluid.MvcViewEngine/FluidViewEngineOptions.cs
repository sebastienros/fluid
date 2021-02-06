using System.Collections.Generic;
using System.Text.Encodings.Web;

namespace Fluid.MvcViewEngine
{
    public class FluidViewEngineOptions
    {
        public IList<string> ViewLocationFormats { get; } = new List<string>();

        public FluidViewParser Parser { get; } = new FluidViewParser();

        /// <summary>
        /// Gets or sets the text encoder to use during rendering.
        /// </summary>
        public TextEncoder TextEncoder = HtmlEncoder.Default;

        public TemplateOptions TemplateOptions { get; } = new TemplateOptions();
    }
}
