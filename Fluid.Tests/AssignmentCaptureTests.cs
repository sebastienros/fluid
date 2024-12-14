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
            var source = @"
                {%- assign a = 'b' %}{{a-}}
            ";

            _parser.TryParse(source, out var template, out var error);
            var context = new TemplateContext
            {
                Assigned = (identifier, value, context) => new StringValue(value.ToStringValue() + "_altered")
            };
            var result = await template.RenderAsync(context);
            Assert.Equal("b_altered", result);
        }
    }
}
