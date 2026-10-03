using System.IO;
using System.Text.Encodings.Web;
using System.Threading.Tasks;
using Fluid.Ast;
using Fluid.Values;
using Xunit;

namespace Fluid.Tests
{
    public class IfChangedStatementTests
    {
        [Fact]
        public async Task IfChangedOutputsOnFirstInvocation()
        {
            var statement = new IfChangedStatement(
                [new OutputStatement(new LiteralExpression(new StringValue("hello")))]
            );

            var context = new TemplateContext();
            var sw = new StringWriter();

            await statement.WriteToAsync(sw, HtmlEncoder.Default, context);

            Assert.Equal("hello", sw.ToString());
        }

        [Fact]
        public async Task IfChangedDoesNotOutputOnSecondIdenticalInvocation()
        {
            var statement = new IfChangedStatement(
                [new OutputStatement(new LiteralExpression(new StringValue("hello")))]
            );

            var context = new TemplateContext();
            var sw = new StringWriter();

            await statement.WriteToAsync(sw, HtmlEncoder.Default, context);
            await statement.WriteToAsync(sw, HtmlEncoder.Default, context);

            Assert.Equal("hello", sw.ToString());
        }

        [Fact]
        public async Task IfChangedOutputsWhenContentChanges()
        {
            // We need to use parsed templates to test dynamic content
            var parser = new FluidParser();
            var template = parser.Parse("{% assign x = 'a' %}{% ifchanged %}{{ x }}{% endifchanged %}{% assign x = 'b' %}{% ifchanged %}{{ x }}{% endifchanged %}");

            var context = new TemplateContext();
            var result = await template.RenderAsync(context);

            Assert.Equal("ab", result);
        }

        [Fact]
        public async Task IfChangedEmptyBlockOutputsOnce()
        {
            var parser = new FluidParser();
            var template = parser.Parse("{% ifchanged %}{% endifchanged %}{% ifchanged %}{% endifchanged %}");

            var context = new TemplateContext();
            var result = await template.RenderAsync(context);

            Assert.Equal("", result);
        }

        [Fact]
        public async Task IfChangedWorksWithinForLoop()
        {
            var parser = new FluidParser();
            var template = parser.Parse("{% assign list = '1,1,2,2,3' | split: ',' %}{% for item in list %}{% ifchanged %}{{ item }}{% endifchanged %}{% endfor %}");

            var context = new TemplateContext();
            var result = await template.RenderAsync(context);

            Assert.Equal("123", result);
        }

        [Fact]
        public async Task IfChangedOutputsDifferentContent()
        {
            var parser = new FluidParser();
            var template = parser.Parse("{% ifchanged %}a{% endifchanged %}{% ifchanged %}b{% endifchanged %}");

            var context = new TemplateContext();
            var result = await template.RenderAsync(context);

            // Both output because content is different ("a" vs "b")
            Assert.Equal("ab", result);
        }

        [Fact]
        public async Task IfChangedRespectsWhitespaceTrimming()
        {
            var parser = new FluidParser();
            var template = parser.Parse("X{%- ifchanged -%}Y{%- endifchanged -%}Z");

            var context = new TemplateContext();
            var result = await template.RenderAsync(context);

            Assert.Equal("XYZ", result);
        }

        [Fact]
        public async Task IfChangedHandlesNilValues()
        {
            var parser = new FluidParser();
            var template = parser.Parse("{% ifchanged %}{{ undefined }}{% endifchanged %}{% ifchanged %}{{ undefined }}{% endifchanged %}");

            var context = new TemplateContext();
            var result = await template.RenderAsync(context);

            // Both output empty string, but first invocation outputs (empty), second doesn't
            Assert.Equal("", result);
        }

        [Fact]
        public async Task IfChangedWithSortedList()
        {
            var parser = new FluidParser();
            var template = parser.Parse("{% assign list = \"1,3,2,1,3,1,2\" | split: \",\" | sort %}{% for item in list -%}{%- ifchanged %} {{ item }}{% endifchanged -%}{%- endfor %}");

            var context = new TemplateContext();
            var result = await template.RenderAsync(context);

            Assert.Equal(" 1 2 3", result);
        }
    }
}
