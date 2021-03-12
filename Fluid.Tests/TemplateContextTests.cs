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
            var parser = new FluidParser();

            parser.TryParse("{{ p.NaMe }}", out var template, out var error);

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
            var parser = new FluidParser();

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
            var parser = new FluidParser();

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
            var parser = new FluidParser();
            var template = parser.Parse("Hi {{Name}}");
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
