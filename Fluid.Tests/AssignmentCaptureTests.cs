using System.Linq;
using Fluid.Values;
using Fluid.Filters;
using Xunit;
using System.Threading.Tasks;
using Fluid.Tests.Extensibility;

namespace Fluid.Tests
{
    public class AssignmentCaptureTests
    {
#if COMPILED
        private static FluidParser _parser = new FluidParser(new FluidParserOptions { AllowFunctions = true }).Compile();
#else
        private static FluidParser _parser = new FluidParser(new FluidParserOptions { AllowFunctions = true });
#endif


        [Fact]
        public async Task Assign()
        {
            string buffer = null;

            var source = @"
                {% assign a = 'b' %}
            ";

            _parser.TryParse(source, out var template, out var error);
            var context = new TemplateContext
            {
                Assigned = (v) => buffer = v.ToStringValue()
            };
            var result = await template.RenderAsync(context);
            Assert.Equal("b", buffer);
        }

        [Fact]
        public async Task Capture()
        {
            string buffer = null;

            var source = @"
                {% capture a %}b{% endcapture %}
            ";

            _parser.TryParse(source, out var template, out var error);
            var context = new TemplateContext
            {
                Assigned = (v) => buffer = v.ToStringValue()
            };
            var result = await template.RenderAsync(context);
            Assert.Equal("b", buffer);
        }
    }
}
