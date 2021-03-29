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
            builder.Services.AddTransient<IConfigureOptions<FluidViewEngineOptions>, FluidViewEngineOptionsSetup>();

            if (setupAction != null)
            {
                builder.Services.Configure(setupAction);
            }

            builder.Services.AddTransient<IConfigureOptions<MvcViewOptions>, FluidMvcViewOptionsSetup>();
            builder.Services.AddSingleton<IFluidRendering, FluidRendering>();
            builder.Services.AddSingleton<IFluidViewEngine, FluidViewEngine>();
            return builder;

        }
    }
}
