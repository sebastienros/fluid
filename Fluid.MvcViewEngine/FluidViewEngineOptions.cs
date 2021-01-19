using System;
using System.Collections.Generic;
using System.Text.Encodings.Web;
using Microsoft.Extensions.FileProviders;

namespace Fluid.MvcViewEngine
{
    public class FluidViewEngineOptions
    {
        public IList<string> ViewLocationFormats { get; } = new List<string>();

        public IFileProvider FileProvider { get; set; }

        public Action<FluidViewParser> Parser { get; set; }

        /// <summary>
        /// Gets or sets the text encoder to use during rendering.
        /// </summary>
        public TextEncoder TextEncoder = HtmlEncoder.Default;
    }
}
