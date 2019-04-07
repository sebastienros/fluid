using System.Collections.Generic;
using System.Text.Encodings.Web;
using Microsoft.Extensions.FileProviders;
using Fluid.Tags;

namespace Fluid.MvcViewEngine
{
    public class FluidViewEngineOptions
    {
        public IList<string> ViewLocationFormats { get; } = new List<string>();

        public IFileProvider FileProvider { get; set; }

        /// <summary>
        /// Gets or sets the text encoder to use during rendering.
        /// </summary>
        public TextEncoder TextEncoder = HtmlEncoder.Default;

        /// <summary>
        /// Registers a tag or block to a <see cref="FluidViewTemplate"/>.
        /// </summary>
        /// <typeparam name="T">The generic <see cref="ITag"/>.</typeparam>
        /// <param name="name">The tag name.</param>
        /// <param name="isBlock">Whether the tag is block or not. Defaults to <code>false</code>.</param>
        public void Add<T>(string name, bool isBlock = false) where T : ITag, new()
        {
            if (isBlock)
                FluidViewTemplate.Factory.RegisterBlock(name, new T());
            else
                FluidViewTemplate.Factory.RegisterTag(name, new T());
        }
    }
}