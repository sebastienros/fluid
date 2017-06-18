using System.Collections.Generic;

namespace FluidMvcViewEngine
{
    public class FluidViewEngineOptions
    {
        public IList<string> ViewLocationFormats { get; } = new List<string>();
    }
}
