using System;
using System.Collections.Generic;
using Microsoft.Extensions.FileProviders;

namespace FluidMvcViewEngine
{
    public class FluidViewEngineOptions
    {
        public IList<string> ViewLocationFormats { get; } = new List<string>();

        public IFileProvider FileProvider { get; set; }

        public TimeSpan CacheDuration { get; set; } = TimeSpan.FromHours(1);
    }
}
