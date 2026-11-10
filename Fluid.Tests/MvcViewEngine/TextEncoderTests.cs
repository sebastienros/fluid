using Fluid.MvcViewEngine;
using Fluid.Tests.Mocks;
using Fluid.ViewEngine;
using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using Microsoft.Extensions.WebEncoders;
using System.IO;
using System.Text.Encodings.Web;
using System.Text.Unicode;
using System.Threading.Tasks;
using Xunit;

namespace Fluid.Tests.MvcViewEngine;

public class TextEncoderTests
{
    [Fact]
    public async Task DefaultEncoderShouldKeepNonAsciiTextAndEncodeHtml()
    {
        var mockFileProvider = new MockFileProvider();
        mockFileProvider.Add("Views/Index.liquid", "{{ text }}");

        var options = new FluidViewEngineOptions
        {
            ViewsFileProvider = new FileProviderMapper(mockFileProvider, "Views"),
        };
        options.ViewsLocationFormats.Add("/{0}" + Constants.ViewExtension);

        var context = new TemplateContext();
        context.SetValue("text", "Café 漢字 <script>a & b \"c\" 'd'</script> 🚀");

        var sw = new StringWriter();
        await new FluidViewRenderer(options).RenderViewAsync(sw, "Index.liquid", context);

        Assert.Equal("Café 漢字 &lt;script&gt;a &amp; b &quot;c&quot; &#x27;d&#x27;&lt;/script&gt; &#x1F680;", sw.ToString());
    }

    [Fact]
    public void MvcShouldUseRegisteredHtmlEncoder()
    {
        var builder = WebApplication.CreateBuilder();
        builder.Services.Configure<WebEncoderOptions>(o => o.TextEncoderSettings = new TextEncoderSettings(UnicodeRanges.BasicLatin, UnicodeRanges.Latin1Supplement));
        builder.Services.AddControllersWithViews().AddFluid();

        using var app = builder.Build();
        var options = app.Services.GetRequiredService<IOptions<FluidMvcViewOptions>>().Value;

        Assert.Same(app.Services.GetRequiredService<HtmlEncoder>(), options.TextEncoder);
        Assert.Equal("Café &#x6F22;", options.TextEncoder.Encode("Café 漢"));
    }

    [Fact]
    public void MvcShouldKeepExplicitTextEncoder()
    {
        var builder = WebApplication.CreateBuilder();
        builder.Services.AddControllersWithViews().AddFluid();
        builder.Services.Configure<FluidMvcViewOptions>(o => o.TextEncoder = NullEncoder.Default);

        using var app = builder.Build();
        var options = app.Services.GetRequiredService<IOptions<FluidMvcViewOptions>>().Value;

        Assert.Same(NullEncoder.Default, options.TextEncoder);
    }

    [Fact]
    public void MinimalApisShouldFallBackToDefaultEncoderWithoutHtmlEncoderService()
    {
        var builder = WebApplication.CreateBuilder();
        builder.Services.AddFluid();

        using var app = builder.Build();
        var options = app.Services.GetRequiredService<IOptions<FluidViewEngineOptions>>().Value;

        Assert.Null(app.Services.GetService<HtmlEncoder>());
        Assert.Same(FluidViewEngineOptions.DefaultTextEncoder, options.TextEncoder);
    }

    [Fact]
    public void MinimalApisShouldUseRegisteredHtmlEncoder()
    {
        var builder = WebApplication.CreateBuilder();
        builder.Services.AddWebEncoders();
        builder.Services.AddFluid();

        using var app = builder.Build();
        var options = app.Services.GetRequiredService<IOptions<FluidViewEngineOptions>>().Value;

        Assert.Same(app.Services.GetRequiredService<HtmlEncoder>(), options.TextEncoder);
    }
}
