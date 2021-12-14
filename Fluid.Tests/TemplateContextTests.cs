using Fluid.Ast;
using Fluid.Values;
using System;
using System.Globalization;
using System.Linq;
using System.Threading.Tasks;
using Xunit;

namespace Fluid.Tests
{
    public class TemplateContextTests
    {
#if COMPILED
        private static FluidParser _parser = new FluidParser().Compile();
#else
        private static FluidParser _parser = new FluidParser();
#endif

        [Fact]
        public async Task ShouldNotThrowException()
        {
            var exception = await Record.ExceptionAsync(() => Task.WhenAll(Enumerable.Range(0, 10).Select(x => Register())));

            Assert.Null(exception);
        }

        private static async Task Register()
        {
            await Task.Delay(10);
            var templateContext = new TemplateContext();
            templateContext.Options.MemberAccessStrategy.Register(typeof(TestClass));
        }

        [Fact]
        public void ScopeShouldFallbackToTemplateOptions()
        {
            _parser.TryParse("{{ p.NaMe }}", out var template, out var error);

            var options = new TemplateOptions();
            options.Scope.SetValue("o1", new StringValue("o1"));
            options.Scope.SetValue("o2", new StringValue("o2"));

            var context = new TemplateContext(options);
            context.SetValue("o2", "new o2");
            context.SetValue("o3", "o3");

            Assert.Equal("o1", context.GetValue("o1").ToStringValue());
            Assert.Equal("new o2", context.GetValue("o2").ToStringValue());
            Assert.Equal("o3", context.GetValue("o3").ToStringValue());
        }

        [Fact]
        public void CustomContextShouldNotUseTemplateOptionsProperties()
        {
            var options = new TemplateOptions();

            var context = new TemplateContext(options);
            context.TimeZone = TimeZoneInfo.Utc;
            context.CultureInfo = new CultureInfo("fr-FR");
            context.Now = () => new DateTime(2020, 01, 01);

            Assert.Equal(TimeZoneInfo.Utc, context.TimeZone);
            Assert.Equal(new CultureInfo("fr-FR"), context.CultureInfo);
            Assert.Equal(new DateTime(2020, 01, 01), context.Now());
        }

        [Fact]
        public void DefaultContextShouldUseTemplateOptionsProperties()
        {
            var options = new TemplateOptions();
            options.TimeZone = TimeZoneInfo.Utc;
            options.CultureInfo = new CultureInfo("fr-FR");
            options.Now = () => new DateTime(2020, 01, 01);

            var context = new TemplateContext(options);

            Assert.Equal(TimeZoneInfo.Utc, context.TimeZone);
            Assert.Equal(new CultureInfo("fr-FR"), context.CultureInfo);
            Assert.Equal(new DateTime(2020, 01, 01), context.Now());
        }

        [Fact]
        public void UseDifferentModelsWithSameMemberName()
        {
            // Arrange
            var template = _parser.Parse("Hi {{Name}}");
            var model1 = new TestClass { Name = "TestClass" };
            var model2 = new AnotherTestClass { Name = "AnotherTestClass" };
            
            // Act
            template.Render(new TemplateContext(model1));
            template.Render(new TemplateContext(model2));
            template.Render(new TemplateContext(model2));
            template.Render(new TemplateContext(model2));
            template.Render(new TemplateContext(model1));
            template.Render(new TemplateContext(model2));
        }

        [Fact]
        public void SegmentAccessorCacheShouldVaryByType()
        {
            // NB: Based on a previous implementation what would cache accessors too aggressively

            FluidParser parser = new();
            var options = new TemplateOptions { MemberAccessStrategy = UnsafeMemberAccessStrategy.Instance };
            var template = parser.Parse("{% if Model1 %}{{ Model1.Name }}{% endif %}");

            var model1 = new { Model1 = new { Name = "model1" } };
            var model2 = new { Model2 = new { Name = "model2" } };

            Assert.Equal("model1", template.Render(new TemplateContext(model1, options)));
            Assert.Equal("", template.Render(new TemplateContext(model2, options)));
            Assert.Equal("model1", template.Render(new TemplateContext(model1, options)));
        }

        [Fact]
        public void TemplateContextShouldBeImmutable()
        {
            _parser.TryParse("{% capture greetings %}Hello {{text1}}{%endcapture%} {% assign foo = 'bar' %}", out var template, out var error);

            var context = new TemplateContext();
            context.SetValue("text1", "World");
            
            template.Render(context);

            Assert.Equal("World", context.GetValue("text1").ToStringValue());
            Assert.DoesNotContain("greetings", context.ValueNames);
            Assert.DoesNotContain("foo", context.ValueNames);
        }

        [Fact]
        public void ScopeSetValueAcceptsNull()
        {
            var context = new TemplateContext();
            context.SetValue("text", null);
            Assert.Equal(NilValue.Instance, context.GetValue("text"));
        }

        [Fact]
        public async Task ShouldNotReleaseScopeAsynchronously()
        {
            var parser = new FluidParser();

            parser.RegisterEmptyBlock("sleep", async (statements, writer, encoder, context) =>
            {
                context.EnterChildScope();
                context.IncrementSteps();
                context.SetValue("id", "0");
                await Task.Delay(100);
                await statements.RenderStatementsAsync(writer, encoder, context);
                context.ReleaseScope();
                return Completion.Normal;
            });

            var context = new TemplateContext { };
            context.SetValue("id", "1");
            var template = parser.Parse(@"{{id}}{%sleep%}{{id}}{%endsleep%}{{id}}");

            var output = await template.RenderAsync(context);

            Assert.Equal("101", output);
        }

        private class TestClass
        {
            public string Name { get; set; }
        }

        private class AnotherTestClass
        {
            public string Name { get; set; }
        }
    }
}
