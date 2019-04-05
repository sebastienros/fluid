using System;
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
                throw new ArgumentNullException(nameof(builder));
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

        public static IMvcBuilder WithTags(this IMvcBuilder builder, Action<FluidTagOptions> setupAction)
        {
            if (builder == null)
            {
                throw new ArgumentNullException(nameof(builder));
            }

            if (setupAction == null)
            {
                throw new ArgumentNullException(nameof(setupAction));
            }

            setupAction.Invoke(new FluidTagOptions());

            return builder;
        }

        public static IMvcBuilder WithBlocks(this IMvcBuilder builder, Action<FluidBlockOptions> setupAction)
        {
            if (builder == null)
            {
                throw new ArgumentNullException(nameof(builder));
            }

            if (setupAction == null)
            {
                throw new ArgumentNullException(nameof(setupAction));
            }

            setupAction.Invoke(new FluidBlockOptions());

            return builder;
        }
    }
}
