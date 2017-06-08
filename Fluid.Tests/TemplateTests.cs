using Xunit;

namespace Fluid.Tests
{
    public class TemplateTests
    {
        [Fact]
        public void ShouldRenderText()
        {
            FluidTemplate.TryParse("Hello World", out var template, out var messages);

            var result = template.Render();
            Assert.Equal("Hello World", result);
        }
    }
}
