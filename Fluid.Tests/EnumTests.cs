using Fluid;
using Fluid.Tests.Domain;
using Fluid.Values;
using Xunit;

namespace Fluid.Tests
{
    public class EnumTests
    {
        [Fact]
        public void EnumInContextShouldRenderAsString()
        {
            var parser = new FluidParser();
            var options = new TemplateOptions();
            var context = new TemplateContext(options);
            
            // Set an enum value directly in context
            context.SetValue("color", Colors.Blue);
            
            var template = parser.Parse("{{color}}");
            var result = template.Render(context);
            
            Assert.Equal("Blue", result);
        }
        
        [Fact]
        public void EnumInArrayShouldRenderAsString()
        {
            var parser = new FluidParser();
            var options = new TemplateOptions();
            var context = new TemplateContext(options);
            
            // Set an array containing enums
            context.SetValue("colors", new[] { Colors.Blue, Colors.Red, Colors.Yellow });
            
            var template = parser.Parse("{% for c in colors %}{{c}} {% endfor %}");
            var result = template.Render(context);
            
            Assert.Equal("Blue Red Yellow ", result);
        }
    }
}
