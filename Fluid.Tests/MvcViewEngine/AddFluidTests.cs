using Fluid.MvcViewEngine;
using Fluid.ViewEngine;
using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using System;
using Xunit;

namespace Fluid.Tests.MvcViewEngine;

public class AddFluidTests
{
    [Fact]
    public void SetupActionShouldConfigureMvcViewOptions()
    {
        var builder = WebApplication.CreateBuilder();
        builder.Services.AddControllersWithViews().AddFluid(o =>
        {
            o.TemplateOptionsBuilder.WithMaxSteps(42);
            o.TextEncoder = NullEncoder.Default;
        });

        using var app = builder.Build();
        var options = app.Services.GetRequiredService<IOptions<FluidMvcViewOptions>>().Value;

        Assert.Same(NullEncoder.Default, options.TextEncoder);
        Assert.Equal(42, options.TemplateOptions.MaxSteps);
    }

    [Fact]
    public void SetupActionShouldConfigureMvcSpecificOptions()
    {
        FluidMvcViewOptions.RenderingMvcViewDelegate rendering = (path, viewContext, context) => default;

        var builder = WebApplication.CreateBuilder();
        builder.Services.AddControllersWithViews().AddFluid(o => o.RenderingViewAsync = rendering);

        using var app = builder.Build();
        var options = app.Services.GetRequiredService<IOptions<FluidMvcViewOptions>>().Value;

        Assert.Same(rendering, options.RenderingViewAsync);
    }

    [Fact]
    public void SetupActionTypedOnBaseOptionsShouldStillApply()
    {
        Action<FluidViewEngineOptions> setup = o => o.TextEncoder = NullEncoder.Default;

        var builder = WebApplication.CreateBuilder();
        builder.Services.AddControllersWithViews().AddFluid(setup);

        using var app = builder.Build();
        var options = app.Services.GetRequiredService<IOptions<FluidMvcViewOptions>>().Value;

        Assert.Same(NullEncoder.Default, options.TextEncoder);
    }
}
