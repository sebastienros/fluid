using Fluid.Tests.Mocks;
using Fluid.ViewEngine;
using System.Collections.Generic;
using System.IO;
using System.Linq;
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

        [Theory]
        [InlineData("Home/Index.liquid")]
        [InlineData("Home\\Index.liquid")]
        public async Task ShouldImportViewStartRecursively(string path)
        {
            _mockFileProvider.Add("Views/Home/Index.liquid", "Hello World");
            _mockFileProvider.Add("Views/Home/_ViewStart.liquid", "ViewStart1");
            _mockFileProvider.Add("Views/_ViewStart.liquid", "ViewStart2");

            var sw = new StringWriter();
            await _renderer.RenderViewAsync(sw, path, new TemplateContext());
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

        [Fact]
        public async Task LayoutShouldBeAbleToIncludeVarsFromViewStart()
        {
            _mockFileProvider.Add("Views/Index.liquid", "{% layout '_Layout' %}[View]{%- assign subtitle = '[SUBTITLE]' -%}");
            _mockFileProvider.Add("Views/_Layout.liquid", "{{title}}{{subtitle}}{% renderbody %}");
            _mockFileProvider.Add("Views/_ViewStart.liquid", "[ViewStart]{%- assign title = '[TITLE]' -%}");

            var sw = new StringWriter();
            await _renderer.RenderViewAsync(sw, "Index.liquid", new TemplateContext());
            await sw.FlushAsync();

            Assert.Equal("[TITLE][SUBTITLE][ViewStart][View]", sw.ToString());
        }

        [Fact]
        public async Task RenderViewOnlyAsyncStream_LargePropertyValue_Nested_SmallBuffer_BiggerThan128LengthString()
        {
            _mockFileProvider.Add("Views/Index.liquid", "{% layout '_Layout' %}{% section bigboy %}{{BigString}}{% endsection %} ");
            _mockFileProvider.Add("Views/_Layout.liquid", "{% rendersection bigboy %}");

            await using var sw = new StreamWriter(new NoSyncStream(), bufferSize: 10);
            var template = new TemplateContext(new { BigString = new string(Enumerable.Range(0, 129).Select(x => 'b').ToArray()) });
            await _renderer.RenderViewAsync(sw, "Index.liquid", template);
            await sw.FlushAsync();
        }

        [Fact]
        public async Task RenderViewOnlyAsyncStream_LargePropertyValue_Nested()
        {
            _mockFileProvider.Add("Views/Index.liquid", "{% layout '_Layout' %}{% section bigboy %}{{BigString}}{% endsection %} ");
            _mockFileProvider.Add("Views/_Layout.liquid", "{% rendersection bigboy %}");

            await using var sw = new StreamWriter(new NoSyncStream());
            var template = new TemplateContext(new { BigString = new string(Enumerable.Range(0, 1500).Select(_ => 'b').ToArray()) });
            await _renderer.RenderViewAsync(sw, "Index.liquid", template);
            await sw.FlushAsync();
        }

        [Fact]
        public async Task ShouldApplyTemplateParsedCallback()
        {
            _mockFileProvider.Add("Views/Index.liquid", "{{ 1 | plus: 2 }}");

            // Use a visitor to replace 2 with 4
            _options.TemplateParsed = (path, template) =>
            {
                var visitor = new Fluid.Tests.Visitors.ReplaceTwosVisitor(Fluid.Values.NumberValue.Create(4));
                return visitor.VisitTemplate(template);
            };

            var sw = new StringWriter();
            await _renderer.RenderViewAsync(sw, "Index.liquid", new TemplateContext());
            await sw.FlushAsync();

            Assert.Equal("5", sw.ToString());

            _options.TemplateParsed = null;
        }

        [Fact]
        public async Task ShouldApplyTemplateParsedCallbackToNestedTemplates()
        {
            _mockFileProvider.Add("Views/Index.liquid", "{% partial 'world' %}");
            _mockFileProvider.Add("Partials/World.liquid", "{{ 1 | plus: 2 }}");

            // Use a visitor to replace 2 with 4
            _options.TemplateParsed = (path, template) =>
            {
                var visitor = new Fluid.Tests.Visitors.ReplaceTwosVisitor(Fluid.Values.NumberValue.Create(4));
                return visitor.VisitTemplate(template);
            };

            var sw = new StringWriter();
            await _renderer.RenderViewAsync(sw, "Index.liquid", new TemplateContext());
            await sw.FlushAsync();

            Assert.Equal("5", sw.ToString());

            _options.TemplateParsed = null;
        }

        [Fact]
        public async Task ShouldApplyTemplateParsedCallbackToViewStarts()
        {
            _mockFileProvider.Add("Views/Index.liquid", "Hello");
            _mockFileProvider.Add("Views/_ViewStart.liquid", "{{ 1 | plus: 2 }} ");

            // Use a visitor to replace 2 with 4
            _options.TemplateParsed = (path, template) =>
            {
                var visitor = new Fluid.Tests.Visitors.ReplaceTwosVisitor(Fluid.Values.NumberValue.Create(4));
                return visitor.VisitTemplate(template);
            };

            var sw = new StringWriter();
            await _renderer.RenderViewAsync(sw, "Index.liquid", new TemplateContext());
            await sw.FlushAsync();

            Assert.Equal("5 Hello", sw.ToString());

            _options.TemplateParsed = null;
        }
    }
}
