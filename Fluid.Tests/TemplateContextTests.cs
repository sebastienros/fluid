using Fluid.Values;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Xunit;

namespace Fluid.Tests
{
    public class TemplateContextTests
    {
        private class TestClass
        {
            public string Name { get; set; }
        }

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
    }
}
