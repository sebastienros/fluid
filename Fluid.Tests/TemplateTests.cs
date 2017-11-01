using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Text.Encodings.Web;
using System.Threading.Tasks;
using Fluid.Tests.Domain;
using Fluid.Tests.Mocks;
using Fluid.Values;
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

        private async Task CheckAsync(string source, string expected, Action<TemplateContext> init = null)
        {
            FluidTemplate.TryParse(source, out var template, out var messages);

            var context = new TemplateContext();
            context.MemberAccessStrategy.Register(new { name = "product 1", price = 1 }.GetType());
            init?.Invoke(context);

            var result = await template.RenderAsync(context);
            Assert.Equal(expected, result);
        }

        [Theory]
        [InlineData("Hello World", "Hello World")]
        public Task ShouldRenderText(string source, string expected)
        {
            return CheckAsync(source, expected);
        }

        [Theory]
        [InlineData("{{ 'abc' }}", "abc")]
        [InlineData("{{ \"abc\" }}", "abc")]
        public Task ShouldEvaluateString(string source, string expected)
        {
            return CheckAsync(source, expected);
        }

        [Theory]
        [InlineData("{{ 'ab''c' }}", "ab&#x27;c", "html")]
        [InlineData("{{ \"a\"\"bc\" }}", "a&quot;bc", "html")]
        [InlineData("{{ '<br />' }}", "&lt;br /&gt;", "html")]
        [InlineData("{{ 'ab''c' }}", "ab%27c", "url")]
        [InlineData("{{ \"a\"\"bc\" }}", "a%22bc", "url")]
        [InlineData("{{ 'a\"\"bc<>&' }}", "a\"\"bc<>&", "null")]
        public async Task ShouldUseEncoder(string source, string expected, string encoderType)
        {
            FluidTemplate.TryParse(source, out var template, out var messages);
            var context = new TemplateContext();
            var sw = new StringWriter();
            TextEncoder encoder = null;
            
            switch (encoderType)
            {
                case "html" : encoder = HtmlEncoder.Default; break;
                case "url" : encoder = UrlEncoder.Default; break;
                case "null" : encoder = NullEncoder.Default; break;
            }

            await template.RenderAsync(sw, encoder, context);
            Assert.Equal(expected, sw.ToString());
        }

        [Theory]
        [InlineData("{{ 'ab''c' | raw}}", "ab'c")]
        [InlineData("{{ \"a\"\"bc\" | raw}}", "a\"bc")]
        [InlineData("{{ '<br />' | raw}}", "<br />")]
        public Task ShouldNotEncodeRawString(string source, string expected)
        {
            return CheckAsync(source, expected);
        }

        [Theory]
        [InlineData("{{ 0 }}", "0")]
        [InlineData("{{ 0 }}", "0")]
        [InlineData("{{ 123 }}", "123")]
        [InlineData("{{ 123.456 }}", "123.456")]
        [InlineData("{{ -123.456 }}", "-123.456")]
        [InlineData("{{ +123.456 }}", "123.456")]
        public Task ShouldEvaluateNumber(string source, string expected)
        {
            return CheckAsync(source, expected);
        }

        [Theory]
        [InlineData("{{ true }}", "true")]
        [InlineData("{{ false }}", "false")]
        public Task ShouldEvaluateBoolean(string source, string expected)
        {
            return CheckAsync(source, expected);
        }

        [Theory]
        [InlineData("{{ 1 | inc }}", "2")]
        [InlineData("{{ 1 | inc | inc }}", "3")]
        [InlineData("{{ 1 | inc:2 | inc }}", "4")]
        [InlineData("{{ 'a' | append:'b', 'c' }}", "abc")]
        public async Task ShouldEvaluateFilters(string source, string expected)
        {
            FluidTemplate.TryParse(source, out var template, out var messages);
            var context = new TemplateContext();

            context.Filters.AddFilter("inc", (i, args, ctx) => 
            {
                var increment = 1;
                if (args.Count > 0)
                {
                    increment = (int)args.At(0).ToNumberValue();
                }

                return new NumberValue(i.ToNumberValue() + increment);
            });

            context.Filters.AddFilter("append", (i, args, ctx) =>
            {
                var s = i.ToStringValue();

                for (var k = 0; k < args.Count; k++)
                {
                    s += args.At(k).ToStringValue();
                }

                return new StringValue(s);
            });

            var result = await template.RenderAsync(context);
            Assert.Equal(expected, result);
        }

        [Fact]
        public async Task ShouldEvaluateBooleanValue()
        {
            FluidTemplate.TryParse("{{ x }}", out var template, out var messages);
            var context = new TemplateContext();
            context.SetValue("x", true);

            var result = await template.RenderAsync(context);
            Assert.Equal("true", result);
        }

        [Fact]
        public async Task ShouldEvaluateStringValue()
        {
            FluidTemplate.TryParse("{{ x }}", out var template, out var messages);
            var context = new TemplateContext();
            context.SetValue("x", "abc");

            var result = await template.RenderAsync(context);
            Assert.Equal("abc", result);
        }

        [Fact]
        public async Task ShouldEvaluateNumberValue()
        {
            FluidTemplate.TryParse("{{ x }}", out var template, out var messages);
            var context = new TemplateContext();
            context.SetValue("x", 1);

            var result = await template.RenderAsync(context);
            Assert.Equal("1", result);
        }

        [Fact]
        public async Task ShouldEvaluateObjectProperty()
        {
            FluidTemplate.TryParse("{{ p.Name }}", out var template, out var messages);

            var context = new TemplateContext();
            context.SetValue("p", new Person { Name = "John" });
            context.MemberAccessStrategy.Register<Person>();

            var result = await template.RenderAsync(context);
            Assert.Equal("John", result);
        }

        [Fact]
        public async Task ShouldEvaluateInheritedObjectProperty()
        {
            FluidTemplate.TryParse("{{ e.Name }} {{ e.Salary }}", out var template, out var messages);

            var context = new TemplateContext();
            context.SetValue("e", new Employee { Name = "John", Salary = 550 });
            context.MemberAccessStrategy.Register<Employee>();

            var result = await template.RenderAsync(context);
            Assert.Equal("John 550", result);
        }

        [Fact]
        public async Task ShouldNotAllowNotRegisteredMember()
        {
            FluidTemplate.TryParse("{{ c.Director.Name }} {{ c.Director.Salary }}", out var template, out var messages);

            var context = new TemplateContext();
            context.SetValue("c", new Company { Director = new Employee { Name = "John", Salary = 550 } });
            context.MemberAccessStrategy.Register<Company>();

            var result = await template.RenderAsync(context);
            Assert.Equal(" ", result);
        }

        [Fact]
        public async Task ShouldOnlyAllowInheritedMember()
        {
            // The Employee class is not registered, hence any access to its properties should return nothing
            // but the Person class is registered, so Name should be available
            FluidTemplate.TryParse("{{ c.Director.Name }} {{ c.Director.Salary }}", out var template, out var messages);

            var context = new TemplateContext();
            context.SetValue("c", new Company { Director = new Employee { Name = "John", Salary = 550 } });
            context.MemberAccessStrategy.Register<Company>();
            context.MemberAccessStrategy.Register<Person>();

            var result = await template.RenderAsync(context);
            Assert.Equal("John ", result);
        }

        [Fact]
        public async Task CanRegisterHierarchy()
        {
            // The Employee class is not registered, hence any access to its properties should return nothing
            // but the Person class is registered, so Name should be available
            FluidTemplate.TryParse("{{ c.Director.Name }} {{ c.Director.Salary }}", out var template, out var messages);

            var context = new TemplateContext();
            context.SetValue("c", new Company { Director = new Employee { Name = "John", Salary = 550 } });
            context.MemberAccessStrategy.Register<Company>();
            context.MemberAccessStrategy.Register<Employee>();
            context.MemberAccessStrategy.Register<Person>();

            var result = await template.RenderAsync(context);
            Assert.Equal("John 550", result);
        }

        [Fact]
        public async Task ShouldEvaluateStringIndex()
        {
            FluidTemplate.TryParse("{{ x[1] }}", out var template, out var messages);
            var context = new TemplateContext();
            context.SetValue("x", "abc");

            var result = await template.RenderAsync(context);
            Assert.Equal("b", result);
        }

        [Theory]
        [InlineData("{% for i in (1..3) %}{{i}}{% endfor %}", "123")]
        [InlineData("{% for p in products %}{{p.price}}{% endfor %}", "123")]
        public Task ShouldEvaluateForStatement(string source, string expected)
        {
            return CheckAsync(source, expected, ctx => { ctx.SetValue("products", _products); });
        }

        [Theory]
        [InlineData(1, "x1")]
        [InlineData(2, "x2")]
        [InlineData(3, "x3")]
        [InlineData(4, "other")]
        public Task ShouldEvaluateElseIfStatement(int x, string expected)
        {
            var template = "{% if x == 1 %}x1{%elsif x == 2%}x2{%elsif x == 3%}x3{%else%}other{% endif %}";

            return CheckAsync(template, expected, ctx => { ctx.SetValue("x", x); });
        }

        [Theory]
        [InlineData(1, "x1")]
        [InlineData(2, "x2")]
        [InlineData(3, "other")]
        public Task ShouldEvaluateCaseStatement(int x, string expected)
        {
            var template = "{% case x %}{%when 1%}x1{%when 2%}x2{%else%}other{% endcase %}";

            return CheckAsync(template, expected, ctx => { ctx.SetValue("x", x); });
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
        public Task ShouldEvaluateCycleStatement(string source, string expected)
        {
            return CheckAsync(source, expected, ctx => { ctx.SetValue("x", 3); });
        }

        [Theory]
        [InlineData("{% assign x = 123 %} {{x}}", "123")]
        public Task ShouldEvaluateAssignStatement(string source, string expected)
        {
            return CheckAsync(source, expected);
        }

        [Theory]
        [InlineData("{% capture x %}Hi there{% endcapture %}{{x}}", "Hi there")]
        public Task ShouldEvaluateCaptureStatement(string source, string expected)
        {
            return CheckAsync(source, expected);
        }

        [Theory]
        [InlineData("{{x == empty}} {{y == empty}}", "false true")]
        public Task ArrayCompareEmptyValue(string source, string expected)
        {
            return CheckAsync(source, expected, ctx =>
            {
                ctx.SetValue("x", new[] { 1, 2, 3 });
                ctx.SetValue("y", new int[0]);
            });
        }

        [Theory]
        [InlineData("{{x == empty}} {{y == empty}}", "false true")]
        public Task DictionaryCompareEmptyValue(string source, string expected)
        {
            return CheckAsync(source, expected, ctx =>
            {
                ctx.SetValue("x", new Dictionary<string, int> { { "1", 1 }, { "2", 2 }, { "3", 3 } });
                ctx.SetValue("y", new Dictionary<string, int>());
            });
        }

        [Theory]
        [InlineData("{{x.size}} {{y.size}}", "3 0")]
        public Task ArrayEvaluatesSize(string source, string expected)
        {
            return CheckAsync(source, expected, ctx =>
            {
                ctx.SetValue("x", new[] { 1, 2, 3 });
                ctx.SetValue("y", new int[0]);
            });
        }

        [Theory]
        [InlineData("{{x.size}} {{y.size}}", "3 0")]
        public Task DictionaryEvaluatesSize(string source, string expected)
        {
            return CheckAsync(source, expected, ctx =>
            {
                ctx.SetValue("x", new Dictionary<string, int> { { "1", 1 }, { "2", 2 }, { "3", 3 } });
                ctx.SetValue("y", new Dictionary<string, int>());
            });
        }

        [Theory]
        [InlineData("{%for x in dic %}{{ x[0] }} {{ x[1] }};{%endfor%}", "a 1;b 2;c 3;")]
        public Task DictionaryIteratesKeyValue(string source, string expected)
        {
            return CheckAsync(source, expected, ctx =>
            {
                ctx.SetValue("dic", new Dictionary<string, int> { { "a", 1 }, { "b", 2 }, { "c", 3 } });
            });
        }

        [Fact]
        public async Task AsyncFiltersAreEvaluated()
        {
            var source = "{% assign result = 'abcd' | query: 'efg' %}{%for x in result %}{{ x }}{%endfor%}";

            FluidTemplate.TryParse(source, out var template, out var messages);

            var context = new TemplateContext();

            context.Filters.AddAsyncFilter("query", async (input, arguments, ctx) =>
            {
                await Task.Delay(10);
                return FluidValue.Create(input.ToStringValue() + arguments.At(0).ToStringValue());
            });

            var result = await template.RenderAsync(context);
            Assert.Equal("abcdefg", result);
        }

        [Theory]
        [InlineData("abc { def", "abc { def")]
        [InlineData("abc } def", "abc } def")]
        [InlineData("abc {{ def", "abc {{ def")]
        [InlineData("abc }} def", "abc }} def")]
        [InlineData("abc {{ def }", "abc {{ def }")]
        [InlineData("abc { def }}", "abc { def }}")]
        [InlineData("abc {% def", "abc {% def")]
        [InlineData("abc %} def", "abc %} def")]
        [InlineData("{% def", "{% def")]
        [InlineData("abc %}", "abc %}")]
        [InlineData("%} def", "%} def")]
        [InlineData("abc {%", "abc {%")]
        [InlineData("abc {{% def", "abc {{% def")]
        [InlineData("abc }%} def", "abc }%} def")]
        public Task ShouldSucceedParseValidTemplate(string source, string expected)
        {
            return CheckAsync(source, expected);
        }

        [Theory]
        [InlineData("{% assign var = 10 %}{% increment var %}{% increment var %}{{ var }}", "0110")]
        [InlineData("{% assign var = 10 %}{% decrement var %}{% decrement var %}{{ var }}", "0-110")]
        public Task IncrementDoesntAffectVariable(string source, string expected)
        {
            return CheckAsync(source, expected);
        }

        [Fact]
        public async Task ModelIsUsedAsFallback()
        {
            var source = "hello {{ firstname }} {{ lastname }}";
            var expected = "hello sebastien ros";

            FluidTemplate.TryParse(source, out var template, out var messages);
            var context = new TemplateContext();
            context.SetValue("firstname", "sebastien");
            context.Model = new { lastname = "ros" };
            context.MemberAccessStrategy.Register(context.Model.GetType());

            var result = await template.RenderAsync(context);
            Assert.Equal(expected, result);
        }

        [Fact]
        public async Task NumbersAreFormattedUsingCulture()
        {
            var source = "{{ 1234.567 }}";
            var expectedFR = "1234,567";
            var expectedUS = "1234.567";

            FluidTemplate.TryParse(source, out var template, out var messages);
            var context = new TemplateContext
            {
                CultureInfo = new CultureInfo("en-US")
            };
            var resultUS = await template.RenderAsync(context);

            context.CultureInfo = new CultureInfo("fr-FR");
            var resultFR = await template.RenderAsync(context);

            Assert.Equal(expectedFR, resultFR);
            Assert.Equal(expectedUS, resultUS);
        }

        [Fact]
        public async Task NumbersAreCultureInvariant()
        {
            var source = "{{ 1234.567 }}";

            var expected = "1234.567";
            CultureInfo.CurrentCulture = new CultureInfo("fr-FR");
            FluidTemplate.TryParse(source, out var templateFR, out var messages);

            CultureInfo.CurrentCulture = new CultureInfo("en-US");
            FluidTemplate.TryParse(source, out var templateUS, out messages);

            var context = new TemplateContext();

            var resultUS = await templateUS.RenderAsync(context);
            var resultFR = await templateFR.RenderAsync(context);

            Assert.Equal(expected, resultFR);
            Assert.Equal(expected, resultUS);
        }

        [Fact]
        public Task IndexersAccessProperties()
        {
            var source = "{% for p in products %}{{p['price']}}{% endfor %}";
            var expected = "123";

            return CheckAsync(source, expected, ctx => { ctx.SetValue("products", _products); });
        }

        [Fact]
        public Task IndexersCanUseVariables()
        {
            var source = "{% assign x = 'price' %} {% for p in products %}{{p[x]}}{% endfor %}";
            var expected = "123";

            return CheckAsync(source, expected, ctx => { ctx.SetValue("products", _products); });
        }

        [Fact]
        public Task CanAccessContextInElseCase()
        {
            var source = "{% if false %}{% else %}{{ products.size }}{% endif %}";
            var expected = "3";

            return CheckAsync(source, expected, ctx => { ctx.SetValue("products", _products); });
        }

        [Theory]
        [InlineData("{{ products | map: 'price' }}", "123")]
        [InlineData("{{ products | map: 'price' | join: ' ' }}", "1 2 3")]
        public Task ShouldProcessMapFilter(string source, string expected)
        {
            return CheckAsync(source, expected, ctx => { ctx.SetValue("products", _products); });
        }

        [Fact]
        public async Task IncludeParamsShouldNotBeSetInTheParentTemplate()
        {
            var source = @"{% include 'Partials', color: 'red', shape: 'circle' %}
{% assign color = 'blue' %}";
            var expected = @"Partial Content
color: 'red'
shape: 'circle'";
            FluidTemplate.TryParse(source, out var template, out var messages);
            var context = new TemplateContext
            {
                FileProvider = new MockFileProvider("Partials")
            };
            var result = await template.RenderAsync(context);

            Assert.Equal(expected, result);
        }

        [Fact]
        public async Task IncludeWithTagParamShouldNotBeSetInTheParentTemplate()
        {
            var source = @"{% include 'Partials' with 'value' %}
{% assign Partials = 'another value' %}
{{ Partials }}";
            var expected = @"Partial Content
color: ''
shape: ''value";
            FluidTemplate.TryParse(source, out var template, out var messages);
            var context = new TemplateContext
            {
                FileProvider = new MockFileProvider("Partials")
            };
            var result = await template.RenderAsync(context);

            Assert.Equal(expected, result);
        }
    }
}
