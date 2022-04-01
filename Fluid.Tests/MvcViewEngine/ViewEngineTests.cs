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

            _options.LayoutsLocationFormats.Clear();
            _options.LayoutsLocationFormats.Add("/Shared/{0}" + Constants.ViewExtension);

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

            Assert.Equal("ViewStart2ViewStart1Hello World", sw.ToString());
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
        public async Task ShouldFindLayoutWithoutExtensionInSharedFolder()
        {
            _mockFileProvider.Add("Views/Index.liquid", "{% layout '_Layout' %}Hi");
            _mockFileProvider.Add("Views/Shared/_Layout.liquid", "SHARED {% renderbody %}");

            var sw = new StringWriter();
            await _renderer.RenderViewAsync(sw, "Index.liquid", new TemplateContext());
            await sw.FlushAsync();

            Assert.Equal("SHARED Hi", sw.ToString());
        }

        [Fact]
        public async Task ShouldFindLayoutWithoutExtensionInViewsFolder()
        {
            _mockFileProvider.Add("Views/Index.liquid", "{% layout '_Layout' %}Hi");
            _mockFileProvider.Add("Views/_Layout.liquid", "LOCAL {% renderbody %}");
            _mockFileProvider.Add("Views/Shared/_Layout.liquid", "SHARED {% renderbody %}");

            var sw = new StringWriter();
            await _renderer.RenderViewAsync(sw, "Index.liquid", new TemplateContext());
            await sw.FlushAsync();

            Assert.Equal("LOCAL Hi", sw.ToString());
        }

        [Fact]
        public async Task ShouldFindLayoutWithExtensionInViewsFolder()
        {
            _mockFileProvider.Add("Views/Index.liquid", "{% layout '_Layout.liquid' %}Hi");
            _mockFileProvider.Add("Views/_Layout.liquid", "LOCAL {% renderbody %}");
            _mockFileProvider.Add("Views/Shared/_Layout.liquid", "SHARED {% renderbody %}");

            var sw = new StringWriter();
            await _renderer.RenderViewAsync(sw, "Index.liquid", new TemplateContext());
            await sw.FlushAsync();

            Assert.Equal("LOCAL Hi", sw.ToString());
        }

        [Fact]
        public async Task ShouldNotFindLayoutWithExtensionInSharedFolder()
        {
            _mockFileProvider.Add("Views/Index.liquid", "{% layout '_Layout.liquid' %}Hi");
            _mockFileProvider.Add("Views/Shared/_Layout.liquid", "SHARED {% renderbody %}");

            var sw = new StringWriter();
            await _renderer.RenderViewAsync(sw, "Index.liquid", new TemplateContext());
            await sw.FlushAsync();

            Assert.Equal("", sw.ToString());
        }

        [Fact]
        public async Task ShouldFindLayoutWithoutExtensionInParentFolder()
        {
            _mockFileProvider.Add("Views/Folder/Index.liquid", "{% layout '_Layout' %}Hi");
            _mockFileProvider.Add("Views/_Layout.liquid", "PARENT {% renderbody %}");

            var sw = new StringWriter();
            await _renderer.RenderViewAsync(sw, "Folder/Index.liquid", new TemplateContext());
            await sw.FlushAsync();

            Assert.Equal("PARENT Hi", sw.ToString());
        }

        [Fact]
        public async Task ShouldFindLayoutWithoutExtensionInLocalFolder()
        {
            _mockFileProvider.Add("Views/Folder/Index.liquid", "{% layout '_Layout' %}Hi");
            _mockFileProvider.Add("Views/Folder/_Layout.liquid", "PARENT {% renderbody %}");

            var sw = new StringWriter();
            await _renderer.RenderViewAsync(sw, "Folder/Index.liquid", new TemplateContext());
            await sw.FlushAsync();

            Assert.Equal("PARENT Hi", sw.ToString());
        }

        [Fact]
        public async Task ShouldNotIncludeViewStartInLayout()
        {
            _mockFileProvider.Add("Views/Index.liquid", "{% layout '_Layout' %}[View]");
            _mockFileProvider.Add("Views/_Layout.liquid", "[Layout]{% renderbody %}");
            _mockFileProvider.Add("Views/_ViewStart.liquid", "[ViewStart]");

            var sw = new StringWriter();
            await _renderer.RenderViewAsync(sw, "Index.liquid", new TemplateContext());
            await sw.FlushAsync();
            Assert.Equal("[Layout][ViewStart][View]", sw.ToString());
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

        [Fact]
        public async Task ShouldApplyViewStartLayoutsRecursively()
        {
            _mockFileProvider.Add("Views/Index.liquid", "Hello World");
            _mockFileProvider.Add("Views/_ViewStart.liquid", "Viewstart 1 {% layout '_layout1.liquid' %}");
            _mockFileProvider.Add("Views/_Layout1.liquid", "Layout 1: {% renderbody %}");

            _mockFileProvider.Add("Views/Home/Index.liquid", "Home Hello World");
            _mockFileProvider.Add("Views/Home/_ViewStart.liquid", "ViewStart 2 {% layout '_layout2.liquid' %}");
            _mockFileProvider.Add("Views/Home/_Layout2.liquid", "Layout 2: {% renderbody %}");


            var sw = new StringWriter();
            await _renderer.RenderViewAsync(sw, "Home/Index.liquid", new TemplateContext());
            await sw.FlushAsync();

            Assert.Equal("Layout 2: Viewstart 1 ViewStart 2 Home Hello World", sw.ToString());
        }
    }
}
