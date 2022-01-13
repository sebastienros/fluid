using Fluid.Tests.Mocks;
using Fluid.ViewEngine;
using System.IO;
using System.Threading.Tasks;
using Xunit;

namespace Fluid.Tests.MvcViewEngine
{
    public class ViewEngineTests
    {
        FluidViewEngineOptions _options = new ();
        FluidViewRenderer _renderer;
        MockFileProvider _mockFileProvider = new ();

        public ViewEngineTests()
        {
            _options.PartialsFileProvider = new FileProviderMapper(_mockFileProvider, "Partials");
            _options.ViewsFileProvider = new FileProviderMapper(_mockFileProvider, "Views");

            _options.TemplateOptions.MemberAccessStrategy = UnsafeMemberAccessStrategy.Instance;

            _options.ViewsLocationFormats.Clear();
            _options.ViewsLocationFormats.Add("/{0}" + Constants.ViewExtension);

            _options.PartialsLocationFormats.Clear();
            _options.PartialsLocationFormats.Add("{0}" + Constants.ViewExtension);
            _options.PartialsLocationFormats.Add("/Partials/{0}" + Constants.ViewExtension);

            _renderer = new FluidViewRenderer(_options);
        }

        [Fact]
        public async Task ShouldRenderView()
        {
            _mockFileProvider.Add("Views/Index.liquid", "Hello World");

            var sw = new StringWriter();
            await _renderer.RenderViewAsync(sw, "Index.liquid", new TemplateContext());
            await sw.FlushAsync();

            Assert.Equal("Hello World", sw.ToString());
        }

        [Fact]
        public async Task ShouldImportViewStart()
        {
            _mockFileProvider.Add("Views/Index.liquid", "Hello World");
            _mockFileProvider.Add("Views/_ViewStart.liquid", "ViewStart");

            var sw = new StringWriter();
            await _renderer.RenderViewAsync(sw, "Index.liquid", new TemplateContext());
            await sw.FlushAsync();

            Assert.Equal("ViewStartHello World", sw.ToString());
        }

        [Fact]
        public async Task ShouldImportViewStartRecursively()
        {
            _mockFileProvider.Add("Views/Home/Index.liquid", "Hello World");
            _mockFileProvider.Add("Views/Home/_ViewStart.liquid", "ViewStart1");
            _mockFileProvider.Add("Views/_ViewStart.liquid", "ViewStart2");

            var sw = new StringWriter();
            await _renderer.RenderViewAsync(sw, "Home/Index.liquid", new TemplateContext());
            await sw.FlushAsync();

            Assert.Equal("ViewStart1ViewStart2Hello World", sw.ToString());
        }

        [Fact]
        public async Task ShouldIncludePartialsUsingSpecifiFileProvider()
        {
            _mockFileProvider.Add("Views/Index.liquid", "Hello {% partial 'world' %}");
            _mockFileProvider.Add("Partials/World.liquid", "World");

            var sw = new StringWriter();
            await _renderer.RenderViewAsync(sw, "Index.liquid", new TemplateContext());
            await sw.FlushAsync();

            Assert.Equal("Hello World", sw.ToString());
        }

        [Fact]
        public async Task ShouldIncludePartialsWithArguments()
        {
            _mockFileProvider.Add("Views/Index.liquid", "Hello {% partial 'world', x: 1 %}");
            _mockFileProvider.Add("Partials/World.liquid", "World {{ x }}");

            var sw = new StringWriter();
            await _renderer.RenderViewAsync(sw, "Index.liquid", new TemplateContext());
            await sw.FlushAsync();

            Assert.Equal("Hello World 1", sw.ToString());
        }

        [Fact]
        public async Task ShouldApplyLayout()
        {
            _mockFileProvider.Add("Views/Index.liquid", "{% layout '_Layout' %}Hi");
            _mockFileProvider.Add("Views/_Layout.liquid", "A {% renderbody %} B");

            var sw = new StringWriter();
            await _renderer.RenderViewAsync(sw, "Index.liquid", new TemplateContext());
            await sw.FlushAsync();

            Assert.Equal("A Hi B", sw.ToString());
        }

        [Fact]
        public async Task ShouldRenderSection()
        {
            _mockFileProvider.Add("Views/Index.liquid", "{% section s %}S1{% endsection %}A {% rendersection s %} B");

            var sw = new StringWriter();
            await _renderer.RenderViewAsync(sw, "Index.liquid", new TemplateContext());
            await sw.FlushAsync();

            Assert.Equal("A S1 B", sw.ToString());
        }

        [Fact]
        public async Task ShouldRenderSectionInLayout()
        {
            _mockFileProvider.Add("Views/Index.liquid", "{% layout '_Layout' %}Hi{% section s %}S1{% endsection %}");
            _mockFileProvider.Add("Views/_Layout.liquid", "A {% rendersection s %} {% renderbody %} B");

            var sw = new StringWriter();
            await _renderer.RenderViewAsync(sw, "Index.liquid", new TemplateContext());
            await sw.FlushAsync();

            Assert.Equal("A S1 Hi B", sw.ToString());
        }

        [Fact]
        public async Task ShouldInvokeRenderingViewAsync()
        {
            _options.RenderingViewAsync = (view, context) => { context.SetValue("custom", "hello"); return default; };

            _mockFileProvider.Add("Views/Index.liquid", "{{ custom }}");

            var sw = new StringWriter();
            await _renderer.RenderViewAsync(sw, "Index.liquid", new TemplateContext());
            await sw.FlushAsync();

            _options.RenderingViewAsync = null;

            Assert.Equal("hello", sw.ToString());
        }
    }
}
