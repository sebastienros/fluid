using System;
using System.Threading.Tasks;
using Fluid.Tests.Mocks;
using Xunit;

namespace Fluid.Tests
{
    public class IndentStatementTests
    {
        MockFileProvider _fileProvider;

        public IndentStatementTests()
        {
            _fileProvider = new MockFileProvider()
                .AddTextFile("singleline.liquid", "this is a single line")
                .AddTextFile("multilines.liquid", $"line1{Environment.NewLine}line2")
                .AddTextFile("emptylines.liquid", $"{Environment.NewLine}{Environment.NewLine}line1{Environment.NewLine}line2");
        }

        [Fact]
        public async Task IndentStatement_ShouldIndentWithTwoSpaces_IfNotParametersAreSet()
        {
            var source = @"{% indent %}{% include 'singleline' %}{% endindent %}";
            var expected = @"  this is a single line";

            var context = new TemplateContext { FileProvider = _fileProvider };
            FluidTemplate.TryParse(source, out var template, out var messages);
            var result = await template.RenderAsync(context);

            Assert.Equal(expected, result);
        }

        [Fact]
        public async Task IndentStatement_ShouldIndentWithSpaces_IfCountIsSet()
        {
            var source = @"{% indent 3 %}{% include 'singleline' %}{% endindent %}";
            var expected = @"   this is a single line";

            var context = new TemplateContext { FileProvider = _fileProvider };
            FluidTemplate.TryParse(source, out var template, out var messages);
            var result = await template.RenderAsync(context);

            Assert.Equal(expected, result);
        }

        [Fact]
        public async Task IndentStatement_ShouldIndentWithCustomSpace_IfCustomSpaceIsSet()
        {
            var source = @"{% indent 3 '\t' %}{% include 'singleline' %}{% endindent %}";
            var expected = "\t\t\tthis is a single line";

            var context = new TemplateContext { FileProvider = _fileProvider };
            FluidTemplate.TryParse(source, out var template, out var messages);
            var result = await template.RenderAsync(context);

            Assert.Equal(expected, result);
        }

        [Fact]
        public async Task IndentStatement_ShouldIndentMultipleLines()
        {
            var source = @"{% indent %}{% include 'multilines' %}{% endindent %}";
            var expected = $"  line1{Environment.NewLine}  line2";

            var context = new TemplateContext { FileProvider = _fileProvider };
            FluidTemplate.TryParse(source, out var template, out var messages);
            var result = await template.RenderAsync(context);

            Assert.Equal(expected, result);
        }


        [Fact]
        public async Task IndentStatement_ShouldNotIndentEmptyLines()
        {
            var source = @"{% indent %}{% include 'emptylines' %}{% endindent %}";
            var expected = $"{Environment.NewLine}{Environment.NewLine}  line1{Environment.NewLine}  line2";

            var context = new TemplateContext { FileProvider = _fileProvider };
            FluidTemplate.TryParse(source, out var template, out var messages);
            var result = await template.RenderAsync(context);

            Assert.Equal(expected, result);
        }
    }
}