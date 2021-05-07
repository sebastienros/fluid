using System;
using Xunit;

namespace Fluid.Tests.Paginating
{
    public class PaginatingTest
    {
        [Fact]
        public static void Page1()
        {
            var parser = new FluidParser();
            var context = new TemplateContext();
            context.SetValue("items", new IdListPaginateValue { CurrentPage = 1 });
            var template = @"{%- paginate items by 10 -%}
{%- for i in items -%}
{{i}},
{%- endfor -%}
{%- endpaginate -%}";
            var statements = parser.Parse(template);
            var result = statements.Render(context);
            Assert.Equal("1,2,3,4,5,6,7,8,9,10,", result);
        }
        [Fact]
        public static void Page2()
        {
            var parser = new FluidParser();
            var context = new TemplateContext();
            context.SetValue("items", new IdListPaginateValue { CurrentPage = 2 });
            var template = @"{%- paginate items by 10 -%}
{%- for i in items -%}
{{i}},
{%- endfor -%}
{%- endpaginate -%}";
            var statements = parser.Parse(template);
            var result = statements.Render(context);
            Assert.Equal("11,12,13,14,15,16,17,18,19,20,", result);
        }
        [Fact]
        public static void ShouldGetAllCount()
        {

            var parser = new FluidParser();
            var context = new TemplateContext();
            context.SetValue("items", new IdListPaginateValue { CurrentPage = 2 });
            var template = @"{{items.size}}";
            var statements = parser.Parse(template);
            var result = statements.Render(context);
            Assert.Equal("100", result);
        }

        [Theory]
        [InlineData("{{ paginate.Items }}", "100")]
        [InlineData("{{ paginate.CurrentPage }}", "2")]
        [InlineData("{{ paginate.CurrentOffset }}", "10")]
        [InlineData("{{ paginate.PageSize }}", "10")]
        [InlineData("{{ paginate.Pages }}", "10")]
        [InlineData("{{ paginate.Previous.IsLink }}", "true")]
        public static void PaginateObjectTest(string express, string expected)
        {
            var parser = new FluidParser();
            var context = new TemplateContext();
            context.SetValue("items", new IdListPaginateValue { CurrentPage = 2 });
            var template = "{% paginate items by 10 %}" + express + "{% endpaginate %}";
            var statements = parser.Parse(template);
            var result = statements.Render(context);
            Assert.Equal(expected, result);
        }
        [Theory]
        [InlineData(1, "<span class=\"deco\">1</span><span class=\"page\"><a href=\"index.aspx?page=2\">2</a></span><span class=\"page\"><a href=\"index.aspx?page=3\">3</a></span><span class=\"deco\">…</span><span class=\"page\"><a href=\"index.aspx?page=10\">10</a></span><span class=\"prev\"><a href=\"index.aspx?page=2\">Next »</a></span>")]
        [InlineData(2, "<span class=\"prev\"><a href=\"index.aspx?page=1\">« Previous</a></span><span class=\"page\"><a href=\"index.aspx?page=1\">1</a></span><span class=\"deco\">2</span><span class=\"page\"><a href=\"index.aspx?page=3\">3</a></span><span class=\"page\"><a href=\"index.aspx?page=4\">4</a></span><span class=\"deco\">…</span><span class=\"page\"><a href=\"index.aspx?page=10\">10</a></span><span class=\"prev\"><a href=\"index.aspx?page=3\">Next »</a></span>")]
        [InlineData(10, "<span class=\"prev\"><a href=\"index.aspx?page=9\">« Previous</a></span><span class=\"page\"><a href=\"index.aspx?page=1\">1</a></span><span class=\"deco\">…</span><span class=\"page\"><a href=\"index.aspx?page=8\">8</a></span><span class=\"page\"><a href=\"index.aspx?page=9\">9</a></span><span class=\"deco\">10</span>")]
        public static void DefaultPaginationFilterTest(Int32 page,string expected)
        {
            var parser = new FluidParser();
            var context = new TemplateContext();
            context.SetValue("items", new IdListPaginateValue { CurrentPage = page });
            var template = "{% paginate items by 10 %}{{ paginate | default_pagination }}{% endpaginate %}";
            var statements = parser.Parse(template);
            var result = statements.Render(context);
            Assert.Equal(result,expected);
        }
    }
}
