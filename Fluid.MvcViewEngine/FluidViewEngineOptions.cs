using System.Collections.Generic;
using System.Text.Encodings.Web;
using Microsoft.Extensions.FileProviders;

namespace FluidMvcViewEngine
{
    public class FluidViewEngineOptions
    {
        public IList<string> ViewLocationFormats { get; } = new List<string>();

        public IFileProvider FileProvider { get; set; }

        /// <summary>
        /// Gets or sets the text encoder to use during rendering.
        /// </summary>
        public TextEncoder TextEncoder = HtmlEncoder.Default;
    }
}
