using System;
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
#if COMPILED
        private static FluidParser _parser = new FluidParser().Compile();
#else
        private static FluidParser _parser = new FluidParser();
#endif

        [Fact]
        public async Task IncludeSatement_ShouldThrowFileNotFoundException_IfTheFileProviderIsNotPresent()
        {
            var expression = new LiteralExpression(new StringValue("_Partial.liquid"));
            var sw = new StringWriter();

            try
            {
                await new IncludeStatement(_parser, expression).WriteToAsync(sw, HtmlEncoder.Default, new TemplateContext());
                Assert.True(false);
            }
            catch (FileNotFoundException)
            {
                return;
            }

            Assert.True(false);
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

            var options = new TemplateOptions() { FileProvider = fileProvider };
            var context = new TemplateContext(options);
            var expectedResult = @"Partial Content
Partials: ''
color: ''
shape: ''";

            await new IncludeStatement(_parser, expression).WriteToAsync(sw, HtmlEncoder.Default, context);

            Assert.Equal(expectedResult, sw.ToString());
        }

        [Fact]
        public async Task IncludeSatement_ShouldLoadCorrectTemplate_IfTheMemberExpressionValueChanges()
        {
            var expression = new MemberExpression(new IdentifierSegment("Firstname"));
            var sw = new StringWriter();

            var fileProvider = new MockFileProvider();
            fileProvider.Add("_First.liquid", @"{{ 'Partial Content One' }}
Partials_One: '{{ Partials }}'
color_One: '{{ color }}'
shape_One: '{{ shape }}'");

            fileProvider.Add("_Second.liquid", @"{{ 'Partial Content Two' }}
Partials_Two: '{{ Partials }}'
color_Two: '{{ color }}'
shape_Two: '{{ shape }}'");

            var model = new Domain.Person { Firstname = "_First.liquid" };

            var options = new TemplateOptions() { FileProvider = fileProvider };
            var context = new TemplateContext(model, options);
            var expectedResultFirstCall = @"Partial Content One
Partials_One: ''
color_One: ''
shape_One: ''";

            var expectedResultSecondCall = @"Partial Content Two
Partials_Two: ''
color_Two: ''
shape_Two: ''";

            var include = new IncludeStatement(_parser, expression);

            await include.WriteToAsync(sw, HtmlEncoder.Default, context);

            Assert.Equal(expectedResultFirstCall, sw.ToString());

            model.Firstname = "_Second.liquid";
            sw = new StringWriter();

            await include.WriteToAsync(sw, HtmlEncoder.Default, context);

            Assert.Equal(expectedResultSecondCall, sw.ToString());
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

            var options = new TemplateOptions() { FileProvider = fileProvider };
            var context = new TemplateContext(options);
            var expectedResult = @"Partial Content
Partials: ''
color: 'blue'
shape: 'circle'";

            await new IncludeStatement(_parser, expression, assignStatements: assignStatements).WriteToAsync(sw, HtmlEncoder.Default, context);

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

            var options = new TemplateOptions() { FileProvider = fileProvider };
            var context = new TemplateContext(options);
            var expectedResult = @"Partial Content
Partials: ''
color: 'blue'
shape: ''";

            await new IncludeStatement(_parser, pathExpression, with: withExpression).WriteToAsync(sw, HtmlEncoder.Default, context);

            Assert.Equal(expectedResult, sw.ToString());
        }

        [Fact]
        public async Task IncludeSatement_ShouldLimitRecursion()
        {
            var expression = new LiteralExpression(new StringValue("_Partial.liquid"));
            var sw = new StringWriter();

            var fileProvider = new MockFileProvider();
            fileProvider.Add("_Partial.liquid", @"{{ 'Partial Content' }} {% include '_Partial' %}");

            var options = new TemplateOptions() { FileProvider = fileProvider };
            var context = new TemplateContext(options);

            await Assert.ThrowsAsync<InvalidOperationException>(() => new IncludeStatement(_parser, expression).WriteToAsync(sw, HtmlEncoder.Default, context).AsTask());
        }
    }
}