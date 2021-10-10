using Fluid.ViewEngine;
using System.Collections.Generic;

namespace Fluid.MvcViewEngine
{
    public class FluidMvcViewOptions : FluidViewEngineOptions
    {
        /// <summary>
        /// Gets les list of view location formats.
        /// </summary>
        /// <remarks>
        /// The first argument '{0}' is the view name.
        /// The second argument '{1}' is the controller name.
        /// The third argument '{2}' is the area name.
        /// </remarks>
        /// <example>
        /// "Views/{1}/{0}"
        /// "Views/Shared/{0}"
        /// </example>
        public IList<string> ViewLocationFormats { get; } = new List<string>();

    }
}
