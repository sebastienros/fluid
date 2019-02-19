using System.Collections.Generic;
using System.IO;
using System.Text.Encodings.Web;
using System.Threading.Tasks;
using Fluid.Ast;
using Fluid.Tests.Mocks;
using Fluid.Values;
using Xunit;

namespace Fluid.Tests
{
    public class IncludeStatementTests
    {
        [Fact]
        public async Task IncludeSatement_ShouldThrowFileNotFoundException_IfTheFileProviderIsNotPresent()
        {
            var expression = new LiteralExpression(new StringValue("_Partial.liquid"));
            var sw = new StringWriter();

            await Assert.ThrowsAsync<FileNotFoundException>(() =>
                new IncludeStatement(expression).WriteToAsync(sw, HtmlEncoder.Default, new TemplateContext())
            );
        }

        [Fact]
        public async Task IncludeSatement_ShouldLoadPartial_IfThePartialsFolderExist()
        {
            var expression = new LiteralExpression(new StringValue("_Partial.liquid"));
            var sw = new StringWriter();

            var fileProvider = new MockFileProvider();
            fileProvider.Add("_Partial.liquid", @"{{ 'Partial Content' }}
Partials: '{{ Partials }}'
color: '{{ color }}'
shape: '{{ shape }}'");

            var context = new TemplateContext
            {
                FileProvider = fileProvider
            };
            var expectedResult = @"Partial Content
Partials: ''
color: ''
shape: ''";

            await new IncludeStatement(expression).WriteToAsync(sw, HtmlEncoder.Default, context);

            Assert.Equal(expectedResult, sw.ToString());
        }

        [Fact]
        public async Task IncludeSatement_WithInlinevariableAssignment_ShouldBeEvaluated()
        {
            var expression = new LiteralExpression(new StringValue("_Partial.liquid"));
            var assignStatements = new List<AssignStatement>
            {
                new AssignStatement("color", new LiteralExpression(new StringValue("blue"))),
                new AssignStatement("shape", new LiteralExpression(new StringValue("circle")))
            };
            var sw = new StringWriter();

            var fileProvider = new MockFileProvider();
            fileProvider.Add("_Partial.liquid", @"{{ 'Partial Content' }}
Partials: '{{ Partials }}'
color: '{{ color }}'
shape: '{{ shape }}'");

            var context = new TemplateContext
            {
                FileProvider = fileProvider
            };
            var expectedResult = @"Partial Content
Partials: ''
color: 'blue'
shape: 'circle'";

            await new IncludeStatement(expression, assignStatements: assignStatements).WriteToAsync(sw, HtmlEncoder.Default, context);

            Assert.Equal(expectedResult, sw.ToString());
        }

        [Fact]
        public async Task IncludeSatement_WithTagParams_ShouldBeEvaluated()
        {
            var pathExpression = new LiteralExpression(new StringValue("color"));
            var withExpression = new LiteralExpression(new StringValue("blue"));
            var sw = new StringWriter();

            var fileProvider = new MockFileProvider();
            fileProvider.Add("color.liquid", @"{{ 'Partial Content' }}
Partials: '{{ Partials }}'
color: '{{ color }}'
shape: '{{ shape }}'");

            var context = new TemplateContext
            {
                FileProvider = fileProvider
            };
            var expectedResult = @"Partial Content
Partials: ''
color: 'blue'
shape: ''";

            await new IncludeStatement(pathExpression, with: withExpression).WriteToAsync(sw, HtmlEncoder.Default, context);

            Assert.Equal(expectedResult, sw.ToString());
        }
    }
}