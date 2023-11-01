using System;
using Fluid.ViewEngine;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;

namespace Fluid.MvcViewEngine
{
    public static class MvcViewFeaturesMvcBuilderExtensions
    {
        public static IMvcBuilder AddFluid(this IMvcBuilder builder, Action<FluidViewEngineOptions> setupAction = null)
        {
            if (builder == null)
            {
                ExceptionHelper.ThrowArgumentNullException(nameof(builder));
            }

            builder.Services.AddOptions();
            builder.Services.AddTransient<IConfigureOptions<FluidMvcViewOptions>, FluidViewEngineOptionsSetup>();

            if (setupAction != null)
            {
                builder.Services.Configure<FluidMvcViewOptions>(setupAction);
                builder.Services.Configure(setupAction);
            }

            builder.Services.AddSingleton<IFluidViewRenderer>(sp =>
            {
                var options = sp.GetRequiredService<IOptions<FluidViewEngineOptions>>().Value;
                return new FluidViewRenderer(options);
            });

            builder.Services.AddTransient<IConfigureOptions<MvcViewOptions>, MvcViewOptionsSetup>();
            builder.Services.AddSingleton<FluidRendering>();
            builder.Services.AddSingleton<IFluidViewEngine, FluidViewEngine>();
            return builder;

        }
    }
}
