using System.Linq;
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
            templateContext.MemberAccessStrategy.Register(typeof(TestClass));
        }
    }
}
