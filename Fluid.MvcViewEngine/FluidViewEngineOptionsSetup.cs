using Microsoft.Extensions.Options;

namespace FluidMvcViewEngine
{
    public class FluidViewEngineOptionsSetup : ConfigureOptions<FluidViewEngineOptions>
    {
        public FluidViewEngineOptionsSetup()
            : base(options => Configure(options))
        {
        }

        private static new void Configure(FluidViewEngineOptions options)
        {            
            options.ViewLocationFormats.Add("Views/{1}/{0}" + FluidViewEngine.ViewExtension);
            options.ViewLocationFormats.Add("Views/Shared/{0}" + FluidViewEngine.ViewExtension);
        }
    }    
}
