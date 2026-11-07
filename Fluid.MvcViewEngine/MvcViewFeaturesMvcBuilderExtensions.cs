using System;
using Fluid.ViewEngine;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;

namespace Fluid.MvcViewEngine;

public static class MvcViewFeaturesMvcBuilderExtensions
{
    /// <summary>
    /// Registers the Fluid view engine.
    /// </summary>
    /// <param name="builder">The MVC builder.</param>
    /// <param name="setupAction">An optional action to configure the <see cref="FluidMvcViewOptions"/> used by the view engine.</param>
    public static IMvcBuilder AddFluid(this IMvcBuilder builder, Action<FluidMvcViewOptions> setupAction = null)
    {
        ArgumentNullException.ThrowIfNull(builder);

        builder.Services.AddOptions();
        builder.Services.AddTransient<IConfigureOptions<FluidMvcViewOptions>, FluidViewEngineOptionsSetup>();

        if (setupAction != null)
        {
            builder.Services.Configure(setupAction);
        }

        builder.Services.AddTransient<IConfigureOptions<MvcViewOptions>, MvcViewOptionsSetup>();
        builder.Services.AddSingleton<FluidRendering>();
        builder.Services.AddSingleton<IFluidViewEngine, FluidViewEngine>();
        return builder;

    }
}
