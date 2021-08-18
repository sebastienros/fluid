using Fluid.MapActionViewEngine;
using Fluid.ViewEngine;
using Microsoft.Extensions.Options;

namespace Microsoft.Extensions.DependencyInjection
{
    public static class MapActionViewEngineBuilderExtensions
    {
        public static IServiceCollection AddFluid(this IServiceCollection services, Action<FluidViewEngineOptions> setupAction = null)
        {
            if (services == null)
            {
                throw new ArgumentNullException(nameof(services));
            }

            services.AddOptions();
            services.AddTransient<IConfigureOptions<FluidViewEngineOptions>, FluidViewEngineOptionsSetup>();

            if (setupAction != null)
            {
                services.Configure(setupAction);
            }

            services.AddSingleton<IFluidViewRenderer>(sp =>
            {
                var options = sp.GetRequiredService<IOptions<FluidViewEngineOptions>>().Value;
                return new FluidViewRenderer(options);
            });

            return services;
        }
    }
}
