using System;
using System.Collections.Generic;
using System.IO;
using System.Text.Encodings.Web;
using Fluid.Ast.Values;
using Fluid.Tests.Domain;
using Xunit;

namespace Fluid.Tests
{
    public class TemplateTests
    {
        private object _products = new []
        {
            new { name = "product 1", price = 1 },
            new { name = "product 2", price = 2 },
            new { name = "product 3", price = 3 },
        };

        private void Check(string source, string expected, Action<TemplateContext> init = null)
        {
            FluidTemplate.TryParse(source, out var template, out var messages);

            var context = new TemplateContext();
            init?.Invoke(context);

            var result = template.Render(context);
            Assert.Equal(expected, result);
        }

        [Theory]
        [InlineData("Hello World", "Hello World")]
        public void ShouldRenderText(string source, string expected)
        {
            Check(source, expected);
        }

        [Theory]
        [InlineData("{{ 'abc' }}", "abc")]
        [InlineData("{{ \"abc\" }}", "abc")]
        public void ShouldEvaluateString(string source, string expected)
        {
            Check(source, expected);
        }

        [Theory]
        [InlineData("{{ 'ab''c' }}", "ab&#x27;c", true)]
        [InlineData("{{ \"a\"\"bc\" }}", "a&quot;bc", true)]
        [InlineData("{{ 'ab''c' }}", "ab%27c", false)]
        [InlineData("{{ \"a\"\"bc\" }}", "a%22bc", false)]
        public void ShouldEncodeString(string source, string expected, bool htmlEncode)
        {
            FluidTemplate.TryParse(source, out var template, out var messages);
            var context = new TemplateContext();
            var sw = new StringWriter();
            TextEncoder encoder = htmlEncode ? (TextEncoder)HtmlEncoder.Default : UrlEncoder.Default;

            template.Render(sw, encoder, context);
            Assert.Equal(expected, sw.ToString());
        }

        [Theory]
        [InlineData("{{ 0 }}", "0")]
        [InlineData("{{ 0 }}", "0")]
        [InlineData("{{ 123 }}", "123")]
        [InlineData("{{ 123.456 }}", "123.456")]
        [InlineData("{{ -123.456 }}", "-123.456")]
        [InlineData("{{ +123.456 }}", "123.456")]
        public void ShouldEvaluateNumber(string source, string expected)
        {
            Check(source, expected);
        }

        [Theory]
        [InlineData("{{ true }}", "true")]
        [InlineData("{{ false }}", "false")]
        public void ShouldEvaluateBoolean(string source, string expected)
        {
            Check(source, expected);
        }

        [Theory]
        [InlineData("{{ 1 | inc }}", "2")]
        [InlineData("{{ 1 | inc | inc }}", "3")]
        [InlineData("{{ 1 | inc:2 | inc }}", "4")]
        [InlineData("{{ 'a' | append:'b', 'c' }}", "abc")]
        public void ShouldEvaluateFilters(string source, string expected)
        {
            FluidTemplate.TryParse(source, out var template, out var messages);
            var context = new TemplateContext();

            context.Filters.Add("inc", (i, args) => 
            {
                var increment = 1;
                if (args.Length > 0)
                {
                    increment = (int)args[0].ToNumberValue();
                }

                return new NumberValue(i.ToNumberValue() + increment);
            });

            context.Filters.Add("append", (i, args) =>
            {
                var s = i.ToStringValue();

                foreach(var a in args)
                {
                    s += a.ToStringValue();
                }

                return new StringValue(s);
            });

            var result = template.Render(context);
            Assert.Equal(expected, result);
        }

        [Fact]
        public void ShouldEvaluateBooleanValue()
        {
            FluidTemplate.TryParse("{{ x }}", out var template, out var messages);
            var context = new TemplateContext();
            context.SetValue("x", true);

            var result = template.Render(context);
            Assert.Equal("true", result);
        }

        [Fact]
        public void ShouldEvaluateStringValue()
        {
            FluidTemplate.TryParse("{{ x }}", out var template, out var messages);
            var context = new TemplateContext();
            context.SetValue("x", "abc");

            var result = template.Render(context);
            Assert.Equal("abc", result);
        }

        [Fact]
        public void ShouldEvaluateNumberValue()
        {
            FluidTemplate.TryParse("{{ x }}", out var template, out var messages);
            var context = new TemplateContext();
            context.SetValue("x", 1);

            var result = template.Render(context);
            Assert.Equal("1", result);
        }

        [Fact]
        public void ShouldEvaluateObjectProperty()
        {
            FluidTemplate.TryParse("{{ p.Name }}", out var template, out var messages);
            var context = new TemplateContext();
            context.SetValue("p", new Person { Name = "John" });

            var result = template.Render(context);
            Assert.Equal("John", result);
        }

        [Fact]
        public void ShouldEvaluateStringIndex()
        {
            FluidTemplate.TryParse("{{ x[1] }}", out var template, out var messages);
            var context = new TemplateContext();
            context.SetValue("x", "abc");

            var result = template.Render(context);
            Assert.Equal("b", result);
        }

        [Theory]
        [InlineData("{% for i in (1..3) %}{{i}}{% endfor %}", "123")]
        [InlineData("{% for p in products %}{{p.price}}{% endfor %}", "123")]
        public void ShouldEvaluateForStatement(string source, string expected)
        {
            Check(source, expected, ctx => { ctx.SetValue("products", _products); });
        }

        [Theory]
        [InlineData(1, "x1")]
        [InlineData(2, "x2")]
        [InlineData(3, "x3")]
        [InlineData(4, "other")]
        public void ShouldEvaluateElseIfStatement(int x, string expected)
        {
            var template = "{% if x == 1 %}x1{%elsif x == 2%}x2{%elsif x == 3%}x3{%else%}other{% endif %}";

            Check(template, expected, ctx => { ctx.SetValue("x", x); });
        }

        [Theory]
        [InlineData(1, "x1")]
        [InlineData(2, "x2")]
        [InlineData(3, "other")]
        public void ShouldEvaluateCaseStatement(int x, string expected)
        {
            var template = "{% case x %}{%when 1%}x1{%when 2%}x2{%else%}other{% endcase %}";

            Check(template, expected, ctx => { ctx.SetValue("x", x); });
        }

        [Theory]
        [InlineData(@"
            {%cycle 'a', 'b'%}
            {%cycle 'a', 'b'%}
            {%cycle 'a', 'b'%}", "\r\naba")]
        [InlineData(@"
            {%cycle x:'a', 'b'%}
            {%cycle 'a', 'b'%}
            {%cycle x:'a', 'b'%}", "\r\naab")]
        public void ShouldEvaluateCycleStatement(string source, string expected)
        {
            Check(source, expected, ctx => { ctx.SetValue("x", 3); });
        }

        [Theory]
        [InlineData("{% assign x = 123 %} {{x}}", "123")]
        public void ShouldEvaluateAssignStatement(string source, string expected)
        {
            Check(source, expected);
        }

        [Theory]
        [InlineData("{% capture x %}Hi there{% endcapture %}{{x}}", "Hi there")]
        public void ShouldEvaluateCaptureStatement(string source, string expected)
        {
            Check(source, expected);
        }

        [Theory]
        [InlineData("{{x == empty}} {{y == empty}}", "false true")]
        public void ArrayCompareEmptyValue(string source, string expected)
        {
            Check(source, expected, ctx =>
            {
                ctx.SetValue("x", new[] { 1, 2, 3 });
                ctx.SetValue("y", new int[0]);
            });
        }

        [Theory]
        [InlineData("{{x == empty}} {{y == empty}}", "false true")]
        public void DictionaryCompareEmptyValue(string source, string expected)
        {
            Check(source, expected, ctx =>
            {
                ctx.SetValue("x", new Dictionary<string, int> { { "1", 1 }, { "2", 2 }, { "3", 3 } });
                ctx.SetValue("y", new Dictionary<string, int>());
            });
        }

        [Theory]
        [InlineData("{{x.size}} {{y.size}}", "3 0")]
        public void ArrayEvaluatesSize(string source, string expected)
        {
            Check(source, expected, ctx =>
            {
                ctx.SetValue("x", new[] { 1, 2, 3 });
                ctx.SetValue("y", new int[0]);
            });
        }

        [Theory]
        [InlineData("{{x.size}} {{y.size}}", "3 0")]
        public void DictionaryEvaluatesSize(string source, string expected)
        {
            Check(source, expected, ctx =>
            {
                ctx.SetValue("x", new Dictionary<string, int> { { "1", 1 }, { "2", 2 }, { "3", 3 } });
                ctx.SetValue("y", new Dictionary<string, int>());
            });
        }

        [Theory]
        [InlineData("{%for x in dic %}{{ x[0] }} {{ x[1] }};{%endfor%}", "a 1;b 2;c 3;")]
        public void DictionaryIteratesKeyValue(string source, string expected)
        {
            Check(source, expected, ctx =>
            {
                ctx.SetValue("dic", new Dictionary<string, int> { { "a", 1 }, { "b", 2 }, { "c", 3 } });
            });
        }

    }
}
