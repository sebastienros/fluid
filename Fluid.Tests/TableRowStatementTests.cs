using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Fluid.Ast;
using Fluid.Values;
using System.Text.Encodings.Web;
using Xunit;

namespace Fluid.Tests
{
    public class TableRowStatementTests
    {
        private static readonly FluidParser _parser = new FluidParser();

        [Fact]
        public async Task ShouldRenderBasicTableRow()
        {
            var e = new TableRowStatement(
                [new OutputStatement(new MemberExpression(new IdentifierSegment("i")))],
                "i",
                new MemberExpression(new IdentifierSegment("items")),
                limit: null,
                offset: null,
                cols: null
            );

            var sw = new StringWriter();
            var context = new TemplateContext();
            context.SetValue("items", new[] { 1, 2, 3 });
            await e.WriteToAsync(sw, HtmlEncoder.Default, context);

            Assert.Equal("<tr class=\"row1\">\n<td class=\"col1\">1</td><td class=\"col2\">2</td><td class=\"col3\">3</td></tr>\n", sw.ToString());
        }

        [Fact]
        public async Task ShouldRenderWithCols()
        {
            var e = new TableRowStatement(
                [new OutputStatement(new MemberExpression(new IdentifierSegment("i")))],
                "i",
                new MemberExpression(new IdentifierSegment("items")),
                limit: null,
                offset: null,
                cols: new LiteralExpression(NumberValue.Create(2))
            );

            var sw = new StringWriter();
            var context = new TemplateContext();
            context.SetValue("items", new[] { 1, 2, 3, 4 });
            await e.WriteToAsync(sw, HtmlEncoder.Default, context);

            Assert.Equal("<tr class=\"row1\">\n<td class=\"col1\">1</td><td class=\"col2\">2</td></tr>\n<tr class=\"row2\"><td class=\"col1\">3</td><td class=\"col2\">4</td></tr>\n", sw.ToString());
        }

        [Fact]
        public async Task ShouldRenderWithLimit()
        {
            var e = new TableRowStatement(
                [new OutputStatement(new MemberExpression(new IdentifierSegment("i")))],
                "i",
                new MemberExpression(new IdentifierSegment("items")),
                limit: new LiteralExpression(NumberValue.Create(2)),
                offset: null,
                cols: null
            );

            var sw = new StringWriter();
            var context = new TemplateContext();
            context.SetValue("items", new[] { 1, 2, 3, 4 });
            await e.WriteToAsync(sw, HtmlEncoder.Default, context);

            Assert.Equal("<tr class=\"row1\">\n<td class=\"col1\">1</td><td class=\"col2\">2</td></tr>\n", sw.ToString());
        }

        [Fact]
        public async Task ShouldRenderWithOffset()
        {
            var e = new TableRowStatement(
                [new OutputStatement(new MemberExpression(new IdentifierSegment("i")))],
                "i",
                new MemberExpression(new IdentifierSegment("items")),
                limit: null,
                offset: new LiteralExpression(NumberValue.Create(2)),
                cols: null
            );

            var sw = new StringWriter();
            var context = new TemplateContext();
            context.SetValue("items", new[] { 1, 2, 3, 4 });
            await e.WriteToAsync(sw, HtmlEncoder.Default, context);

            Assert.Equal("<tr class=\"row1\">\n<td class=\"col1\">3</td><td class=\"col2\">4</td></tr>\n", sw.ToString());
        }

        [Fact]
        public async Task ShouldRenderWithRange()
        {
            var e = new TableRowStatement(
                [new OutputStatement(new MemberExpression(new IdentifierSegment("i")))],
                "i",
                new RangeExpression(
                    new LiteralExpression(NumberValue.Create(1)),
                    new LiteralExpression(NumberValue.Create(4))
                ),
                limit: null,
                offset: null,
                cols: new LiteralExpression(NumberValue.Create(2))
            );

            var sw = new StringWriter();
            await e.WriteToAsync(sw, HtmlEncoder.Default, new TemplateContext());

            Assert.Equal("<tr class=\"row1\">\n<td class=\"col1\">1</td><td class=\"col2\">2</td></tr>\n<tr class=\"row2\"><td class=\"col1\">3</td><td class=\"col2\">4</td></tr>\n", sw.ToString());
        }

        [Fact]
        public async Task ShouldProvideTableRowLoopVariables()
        {
            _parser.TryParse("{% tablerow i in (1..2) %}col:{{ tablerowloop.col }} col0:{{ tablerowloop.col0 }} row:{{ tablerowloop.row }} first:{{ tablerowloop.first }} last:{{ tablerowloop.last }} index:{{ tablerowloop.index }} index0:{{ tablerowloop.index0 }} rindex:{{ tablerowloop.rindex }} rindex0:{{ tablerowloop.rindex0 }} length:{{ tablerowloop.length }} col_first:{{ tablerowloop.col_first }} col_last:{{ tablerowloop.col_last }}{% endtablerow %}", out var template, out var error);

            var result = await template.RenderAsync();

            Assert.Contains("col:1", result);
            Assert.Contains("col0:0", result);
            Assert.Contains("row:1", result);
            Assert.Contains("first:true", result);
            Assert.Contains("length:2", result);
            Assert.Contains("col_first:true", result);
        }

        [Fact]
        public async Task ShouldHandleEmptyCollection()
        {
            var e = new TableRowStatement(
                [new OutputStatement(new MemberExpression(new IdentifierSegment("i")))],
                "i",
                new MemberExpression(new IdentifierSegment("items")),
                limit: null,
                offset: null,
                cols: null
            );

            var sw = new StringWriter();
            var context = new TemplateContext();
            context.SetValue("items", System.Array.Empty<int>());
            await e.WriteToAsync(sw, HtmlEncoder.Default, context);

            Assert.Equal("", sw.ToString());
        }

        [Fact]
        public async Task ShouldHandleBreak()
        {
            _parser.TryParse("{% tablerow i in (1..5) cols:2 %}{{ i }}{% if i == 2 %}{% break %}{% endif %}{% endtablerow %}", out var template, out var error);

            var result = await template.RenderAsync();

            Assert.Equal("<tr class=\"row1\">\n<td class=\"col1\">1</td><td class=\"col2\">2</td></tr>\n", result);
        }

        [Fact]
        public async Task ShouldParseTableRowTag()
        {
            _parser.TryParse("{% tablerow item in collection %}{{ item }}{% endtablerow %}", out var template, out var error);

            Assert.Null(error);
            Assert.NotNull(template);

            var context = new TemplateContext();
            context.SetValue("collection", new[] { "a", "b", "c" });
            var result = await template.RenderAsync(context);

            Assert.Equal("<tr class=\"row1\">\n<td class=\"col1\">a</td><td class=\"col2\">b</td><td class=\"col3\">c</td></tr>\n", result);
        }

        [Fact]
        public async Task ShouldParseTableRowWithColsParameter()
        {
            _parser.TryParse("{% tablerow item in collection cols:2 %}{{ item }}{% endtablerow %}", out var template, out var error);

            Assert.Null(error);
            Assert.NotNull(template);

            var context = new TemplateContext();
            context.SetValue("collection", new[] { "a", "b", "c", "d" });
            var result = await template.RenderAsync(context);

            Assert.Equal("<tr class=\"row1\">\n<td class=\"col1\">a</td><td class=\"col2\">b</td></tr>\n<tr class=\"row2\"><td class=\"col1\">c</td><td class=\"col2\">d</td></tr>\n", result);
        }

        [Fact]
        public async Task ShouldParseTableRowWithLimitParameter()
        {
            _parser.TryParse("{% tablerow item in collection limit:2 %}{{ item }}{% endtablerow %}", out var template, out var error);

            Assert.Null(error);
            Assert.NotNull(template);

            var context = new TemplateContext();
            context.SetValue("collection", new[] { "a", "b", "c", "d" });
            var result = await template.RenderAsync(context);

            Assert.Equal("<tr class=\"row1\">\n<td class=\"col1\">a</td><td class=\"col2\">b</td></tr>\n", result);
        }

        [Fact]
        public async Task ShouldParseTableRowWithOffsetParameter()
        {
            _parser.TryParse("{% tablerow item in collection offset:2 %}{{ item }}{% endtablerow %}", out var template, out var error);

            Assert.Null(error);
            Assert.NotNull(template);

            var context = new TemplateContext();
            context.SetValue("collection", new[] { "a", "b", "c", "d" });
            var result = await template.RenderAsync(context);

            Assert.Equal("<tr class=\"row1\">\n<td class=\"col1\">c</td><td class=\"col2\">d</td></tr>\n", result);
        }

        [Fact]
        public async Task ShouldParseTableRowWithRange()
        {
            _parser.TryParse("{% tablerow i in (1..4) cols:2 %}{{ i }}{% endtablerow %}", out var template, out var error);

            Assert.Null(error);
            Assert.NotNull(template);

            var result = await template.RenderAsync();

            Assert.Equal("<tr class=\"row1\">\n<td class=\"col1\">1</td><td class=\"col2\">2</td></tr>\n<tr class=\"row2\"><td class=\"col1\">3</td><td class=\"col2\">4</td></tr>\n", result);
        }

        [Fact]
        public async Task ShouldParseTableRowWithAllParameters()
        {
            _parser.TryParse("{% tablerow i in (1..10) cols:2 limit:4 offset:2 %}{{ i }}{% endtablerow %}", out var template, out var error);

            Assert.Null(error);
            Assert.NotNull(template);

            var result = await template.RenderAsync();

            Assert.Equal("<tr class=\"row1\">\n<td class=\"col1\">3</td><td class=\"col2\">4</td></tr>\n<tr class=\"row2\"><td class=\"col1\">5</td><td class=\"col2\">6</td></tr>\n", result);
        }

        [Fact]
        public async Task ColsShouldTruncateFloatToInt()
        {
            _parser.TryParse("{% tablerow i in (1..4) cols:2.6 %}{{ i }}{% endtablerow %}", out var template, out var error);

            Assert.Null(error);
            Assert.NotNull(template);

            var result = await template.RenderAsync();

            // 2.6 truncates to 2
            Assert.Equal("<tr class=\"row1\">\n<td class=\"col1\">1</td><td class=\"col2\">2</td></tr>\n<tr class=\"row2\"><td class=\"col1\">3</td><td class=\"col2\">4</td></tr>\n", result);
        }

        [Fact]
        public async Task ShouldHandleOddNumberOfItemsWithCols()
        {
            _parser.TryParse("{% tablerow i in (1..5) cols:2 %}{{ i }}{% endtablerow %}", out var template, out var error);

            Assert.Null(error);
            Assert.NotNull(template);

            var result = await template.RenderAsync();

            Assert.Equal("<tr class=\"row1\">\n<td class=\"col1\">1</td><td class=\"col2\">2</td></tr>\n<tr class=\"row2\"><td class=\"col1\">3</td><td class=\"col2\">4</td></tr>\n<tr class=\"row3\"><td class=\"col1\">5</td></tr>\n", result);
        }

        [Fact]
        public async Task TableRowLoopColLastShouldBeCorrect()
        {
            _parser.TryParse("{% tablerow i in (1..4) cols:2 %}{{ tablerowloop.col_last }}{% endtablerow %}", out var template, out var error);

            var result = await template.RenderAsync();

            Assert.Equal("<tr class=\"row1\">\n<td class=\"col1\">false</td><td class=\"col2\">true</td></tr>\n<tr class=\"row2\"><td class=\"col1\">false</td><td class=\"col2\">true</td></tr>\n", result);
        }

        [Fact]
        public async Task TableRowLoopColFirstShouldBeCorrect()
        {
            _parser.TryParse("{% tablerow i in (1..4) cols:2 %}{{ tablerowloop.col_first }}{% endtablerow %}", out var template, out var error);

            var result = await template.RenderAsync();

            Assert.Equal("<tr class=\"row1\">\n<td class=\"col1\">true</td><td class=\"col2\">false</td></tr>\n<tr class=\"row2\"><td class=\"col1\">true</td><td class=\"col2\">false</td></tr>\n", result);
        }

        [Fact]
        public async Task TableRowLoopRowShouldIncrement()
        {
            _parser.TryParse("{% tablerow i in (1..4) cols:2 %}{{ tablerowloop.row }}{% endtablerow %}", out var template, out var error);

            var result = await template.RenderAsync();

            Assert.Equal("<tr class=\"row1\">\n<td class=\"col1\">1</td><td class=\"col2\">1</td></tr>\n<tr class=\"row2\"><td class=\"col1\">2</td><td class=\"col2\">2</td></tr>\n", result);
        }
    }
}
