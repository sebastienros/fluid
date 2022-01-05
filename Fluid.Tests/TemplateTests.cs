using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text.Encodings.Web;
using System.Threading.Tasks;
using Fluid.Parser;
using Fluid.Tests.Domain;
using Fluid.Tests.Domain.WithInterfaces;
using Fluid.Tests.Mocks;
using Fluid.Values;
using Xunit;

namespace Fluid.Tests
{
    public class TemplateTests
    {
#if COMPILED
        private static FluidParser _parser = new FluidParser().Compile();
#else
        private static FluidParser _parser = new FluidParser();
#endif

        private object _products = new []
        {
            new { name = "product 1", price = 1 },
            new { name = "product 2", price = 2 },
            new { name = "product 3", price = 3 },
        };

        private async Task CheckAsync(string source, string expected, Action<TemplateContext> init = null)
        {
            Assert.True(_parser.TryParse(source, out var template, out var error));

            var context = new TemplateContext();
            context.Options.MemberAccessStrategy.Register(new { name = "product 1", price = 1 }.GetType());
            init?.Invoke(context);

            var result = await template.RenderAsync(context);
            Assert.Equal(expected, result);
        }

        private async Task CheckAsync(string source, string expected, TemplateContext context, TextEncoder encoder)
        {
            _parser.TryParse(source, out var template, out var error);

            var result = await template.RenderAsync(context, encoder);
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
        [InlineData("{{ 'ab\\'c' }}", "ab&#x27;c", "html")]
        [InlineData("{{ \"a\\\"bc\" }}", "a&quot;bc", "html")]
        [InlineData("{{ '<br />' }}", "&lt;br /&gt;", "html")]
        [InlineData("{{ 'ab\\'c' }}", "ab%27c", "url")]
        [InlineData("{{ \"a\\\"bc\" }}", "a%22bc", "url")]
        [InlineData("{{ 'a\"\"bc<>&' }}", "a\"\"bc<>&", "null")]
        public Task ShouldUseEncoder(string source, string expected, string encoderType)
        {
            var context = new TemplateContext();
            TextEncoder encoder = null;
            
            switch (encoderType)
            {
                case "html" : encoder = HtmlEncoder.Default; break;
                case "url" : encoder = UrlEncoder.Default; break;
                case "null" : encoder = NullEncoder.Default; break;
            }

            return CheckAsync(source, expected, context, encoder);
        }

        [Theory]
        [InlineData("{{ 'ab\\'c' | raw}}", "ab'c")]
        [InlineData("{{ \"a\\\"bc\" | raw}}", "a\"bc")]
        [InlineData("{{ '<br />' | raw}}", "<br />")]
        public async Task ShouldNotEncodeRawString(string source, string expected)
        {
            _parser.TryParse(source, out var template, out var error);
            var result = await template.RenderAsync(new TemplateContext(), HtmlEncoder.Default);
            Assert.Equal(expected, result);
        }

        [Theory]
        [InlineData("{% for i in (1..3) %}<br />{% endfor %}", "<br /><br /><br />")]
        public async Task ShouldNotEncodeBlocks(string source, string expected)
        {
            _parser.TryParse(source, out var template, out var error);
            var result = await template.RenderAsync(new TemplateContext(), HtmlEncoder.Default);
            Assert.Equal(expected, result);
        }

        [Theory]
        [InlineData("{% capture foo %}<br />{% endcapture %}{{ foo }}", "<br />")]
        [InlineData("{% capture foo %}{{ '<br />' }}{% endcapture %}{{ foo }}", "&lt;br /&gt;")]
        public async Task ShouldNotEncodeCaptures(string source, string expected)
        {
            _parser.TryParse(source, out var template, out var error);
            var result = await template.RenderAsync(new TemplateContext(), HtmlEncoder.Default);
            Assert.Equal(expected, result);
        }

        [Fact]
        public async Task ShouldCustomizeCaptures()
        {
            _parser.TryParse("{% capture foo %}hello <br /> world{% endcapture %}{{ foo }}", out var template, out var error);
            var result = await template.RenderAsync(new TemplateContext { Captured = (identifier, captured) => new ValueTask<string>(captured.ToUpper()) }, HtmlEncoder.Default);
            Assert.Equal("HELLO <BR /> WORLD", result);
        }

        [Theory]
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
            _parser.TryParse(source, out var template, out var error);
            var options = new TemplateOptions();

            var context = new TemplateContext(options);

            options.Filters.AddFilter("inc", (i, args, ctx) => 
            {
                var increment = 1;
                if (args.Count > 0)
                {
                    increment = (int)args.At(0).ToNumberValue();
                }

                return NumberValue.Create(i.ToNumberValue() + increment);
            });

            options.Filters.AddFilter("append", (i, args, ctx) =>
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
            _parser.TryParse("{{ x }}", out var template, out var error);

            var context = new TemplateContext();
            context.SetValue("x", true);

            var result = await template.RenderAsync(context);
            Assert.Equal("true", result);
        }

        [Fact]
        public async Task ShouldEvaluateStringValue()
        {
            _parser.TryParse("{{ x }}", out var template, out var error);
            var context = new TemplateContext();
            context.SetValue("x", "abc");

            var result = await template.RenderAsync(context);
            Assert.Equal("abc", result);
        }

        [Fact]
        public async Task ShouldEvaluateNumberValue()
        {
            _parser.TryParse("{{ x }}", out var template, out var error);
            var context = new TemplateContext();
            context.SetValue("x", 1);

            var result = await template.RenderAsync(context);
            Assert.Equal("1", result);
        }

        [Fact]
        public async Task ShouldEvaluateObjectProperty()
        {
            _parser.TryParse("{{ p.Firstname }}", out var template, out var error);

            var options = new TemplateOptions();
            options.MemberAccessStrategy.Register<Person>();

            var context = new TemplateContext(options);
            context.SetValue("p", new Person { Firstname = "John" });
            

            var result = await template.RenderAsync(context);
            Assert.Equal("John", result);
        }

        [Fact]
        public async Task ShouldEvaluateObjectPropertyWhenInterfaceRegisteredAsGlobal()
        {
            var options = new TemplateOptions();
            options.MemberAccessStrategy.Register<IAnimal>();

            _parser.TryParse("{{ p.Age }}", out var template, out var error);

            var context = new TemplateContext(options);
            context.SetValue("p", new Dog { Age = 12 });

            var result = await template.RenderAsync(context);
            Assert.Equal("12", result);
        }

        [Fact]
        public async Task ShouldRegisterValueMappingWithInterface()
        {

            _parser.TryParse("{{ p.Name }}", out var template, out var messages);

            var options = new TemplateOptions();
            options.ValueConverters.Add(x => x is IPet pet ? new PetValue(pet) : null);
            var context = new TemplateContext(options);
            context.SetValue("p", new Dog { Name = "Rex" });

            var result = await template.RenderAsync(context);
            Assert.Equal("Rex", result);
        }

        [Fact]
        public async Task ShouldNotAllowNotRegisteredInterfaceMembers()
        {
            var options = new TemplateOptions();
            options.MemberAccessStrategy.Register<IAnimal>();

            _parser.TryParse("{{ p.Name }}", out var template, out var error);

            var context = new TemplateContext(options);
            context.SetValue("p", new Dog { Name = "Rex" });

            var result = await template.RenderAsync(context);
            Assert.Equal("", result);
        }

        [Fact]
        public async Task ShouldEvaluateObjectPropertyWhenInterfaceRegistered()
        {
            _parser.TryParse("{{ p.Name }}", out var template, out var error);

            var options = new TemplateOptions();
            var context = new TemplateContext(options);
            context.SetValue("p", new Dog { Name = "John" });
            options.MemberAccessStrategy.Register<IDog>();

            var result = await template.RenderAsync(context);
            Assert.Equal("John", result);
        }

        [Fact]
        public async Task ShouldEvaluateInheritedObjectProperty()
        {
            _parser.TryParse("{{ e.Firstname }} {{ e.Salary }}", out var template, out var error);

            var options = new TemplateOptions();
            var context = new TemplateContext(options);
            context.SetValue("e", new Employee { Firstname = "John", Salary = 550 });
            options.MemberAccessStrategy.Register<Employee>();

            var result = await template.RenderAsync(context);
            Assert.Equal("John 550", result);
        }

        [Fact]
        public async Task ShouldNotAllowNotRegisteredMember()
        {
            _parser.TryParse("{{ c.Director.Firstname }} {{ c.Director.Salary }}", out var template, out var error);

            var options = new TemplateOptions();
            var context = new TemplateContext(options);
            context.SetValue("c", new Company { Director = new Employee { Firstname = "John", Salary = 550 } });
            options.MemberAccessStrategy.Register<Company>();

            var result = await template.RenderAsync(context);
            Assert.Equal(" ", result);
        }

        [Fact]
        public async Task ShouldOnlyAllowInheritedMember()
        {
            // The Employee class is not registered, hence any access to its properties should return nothing
            // but the Person class is registered, so Name should be available
            _parser.TryParse("{{ c.Director.Firstname }} {{ c.Director.Salary }}", out var template, out var error);

            var options = new TemplateOptions();
            var context = new TemplateContext(options);
            context.SetValue("c", new Company { Director = new Employee { Firstname = "John", Salary = 550 } });
            options.MemberAccessStrategy.Register<Company>();
            options.MemberAccessStrategy.Register<Person>();

            var result = await template.RenderAsync(context);
            Assert.Equal("John ", result);
        }

        [Fact]
        public async Task ShouldEvaluateStringIndex()
        {
            _parser.TryParse("{{ x[1] }}", out var template, out var error);
            var context = new TemplateContext();
            context.SetValue("x", "abc");

            var result = await template.RenderAsync(context);
            Assert.Equal("b", result);
        }

        [Fact]
        public async Task ShouldEvaluateCustomObjectIndex()
        {
            var options = new TemplateOptions();
            options.ValueConverters.Add(o => o is Person p ? new PersonValue(p) : null);

            var context = new TemplateContext(options);
            context.SetValue("p", new Person { Firstname = "Bill" } );

            _parser.TryParse("{{ p[1] }} {{ p['blah'] }}", out var template, out var error);
            var result = await template.RenderAsync(context);
            Assert.Equal("Bill 1 Bill blah", result);
        }

        [Fact]
        public async Task FirstLastSizeShouldUseGetValue()
        {
            var options = new TemplateOptions();
            var context = new TemplateContext(options);
            context.SetValue("p", new PersonValue(new Person()));

            _parser.TryParse("{{ p | size }} {{ p | first }} {{ p | last }}", out var template, out var error);
            var result = await template.RenderAsync(context);
            Assert.Equal("123 456 789", result);
        }

        private class PersonValue : ObjectValueBase
        {
            public PersonValue(Person value) : base(value)
            {
            }

            public override ValueTask<FluidValue> GetIndexAsync(FluidValue index, TemplateContext context)
            {
                return Create(((Person)Value).Firstname + " " + index.ToStringValue(), context.Options);
            }

            public override ValueTask<FluidValue> GetValueAsync(string name, TemplateContext context)
            {
                return name switch
                {
                    "size" => NumberValue.Create(123),
                    "first" => NumberValue.Create(456),
                    "last" => NumberValue.Create(789),
                    _ => NilValue.Instance
                };
            }
        }

        [Theory]
        [InlineData("{% assign my_array = 'abc,123' | split: ',' %}{{ my_array | reverse | join: ',' }}", "123,abc")]
        public Task ShouldReverseArray(string source, string expected)
        {
            return CheckAsync(source, expected);
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
            var template = "{% case x %}{% when 1 %}x1{% when 2 %}x2{% else %}other{% endcase %}";

            return CheckAsync(template, expected, ctx => { ctx.SetValue("x", x); });
        }

        [Theory]
        [InlineData(@"{%cycle 'a', 'b'%}{%cycle 'a', 'b'%}{%cycle 'a', 'b'%}", "aba")]
        [InlineData(@"{%cycle x:'a', 'b'%}{%cycle 'a', 'b'%}{%cycle x:'a', 'b'%}", "aab")]
        [InlineData(@"{%cycle 2:'a', 'b'%}{%cycle '2': 'a', 'b'%}", "ab")]
        [InlineData(@"{%cycle 'a', 'b'%}{%cycle foo: 'a', 'b'%}", "ab")]
        public Task ShouldEvaluateCycleStatement(string source, string expected)
        {
            return CheckAsync(source, expected, ctx => { ctx.SetValue("x", 3); });
        }

        [Theory]
        [InlineData("{% assign x = 123 %} {{x}}", " 123")]
        public Task ShouldEvaluateAssignStatement(string source, string expected)
        {
            return CheckAsync(source, expected);
        }

        [Theory]
        [InlineData("{% capture x %}Hi there{% endcapture %}{{x}}", "Hi there")]
        [InlineData("{% capture x %}some <b>bold</b> statement{% endcapture %}{{x}}", "some <b>bold</b> statement")]
        public Task ShouldEvaluateCaptureStatement(string source, string expected)
        {
            return CheckAsync(source, expected);
        }

        [Theory]
        [InlineData(@"{% capture string_with_newlines %}
hello
there
turtle
{% endcapture %}{{string_with_newlines | strip_newlines}}", "hellothereturtle")]
        public Task CaptureStringNewLines(string source, string expected)
        {
            return CheckAsync(source, expected);
        }


        [Theory]
        [InlineData("{%if x == empty%}true{%else%}false{%endif%} {%if y == empty%}true{%else%}false{%endif%}", "false true")]
        public Task ArrayCompareEmptyValue(string source, string expected)
        {
            return CheckAsync(source, expected, ctx =>
            {
                ctx.SetValue("x", new[] { 1, 2, 3 });
                ctx.SetValue("y", new int[0]);
            });
        }

        [Theory]
        [InlineData("{%if x == empty%}true{%else%}false{%endif%} {%if y == empty%}true{%else%}false{%endif%}", "false true")]
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

            _parser.TryParse(source, out var template, out var error);

            var options = new TemplateOptions();
            var context = new TemplateContext(options);

            options.Filters.AddFilter("query", async (input, arguments, ctx) =>
            {
                await Task.Delay(10);
                return FluidValue.Create(input.ToStringValue() + arguments.At(0).ToStringValue(), options);
            });

            var result = await template.RenderAsync(context);
            Assert.Equal("abcdefg", result);
        }

        [Theory]
        [InlineData("abc { def", "abc { def")]
        [InlineData("abc } def", "abc } def")]
        [InlineData("abc }} def", "abc }} def")]
        [InlineData("abc { def }}", "abc { def }}")]
        [InlineData("abc %} def", "abc %} def")]
        [InlineData("abc %}", "abc %}")]
        [InlineData("%} def", "%} def")]
        [InlineData("abc }%} def", "abc }%} def")]
        public Task ShouldSucceedParseValidTemplate(string source, string expected)
        {
            return CheckAsync(source, expected);
        }

        [Theory]
        [InlineData("abc {{ def", ErrorMessages.ExpectedOutputEnd)]
        [InlineData("abc {{ def }", ErrorMessages.ExpectedOutputEnd)]
        [InlineData("abc {%", ErrorMessages.IdentifierAfterTagStart)]
        [InlineData("abc {{", ErrorMessages.LogicalExpressionStartsFilter)]
        public void ShouldDetectInvalidTemplate(string source, string expected)
        {
            _parser.TryParse(source, out var template, out var error);
            Assert.StartsWith(expected, error);
        }

        [Theory]
        [InlineData("abc {% def")]
        [InlineData("{% def")]
        public void ShouldFailInvalidTemplate(string source)
        {
            _parser.TryParse(source, out var template, out var error);
            Assert.NotEmpty(error);
        }
        [Theory]
        [InlineData("abc {{% def")]
        public void ShouldNotParseInvalidTemplate(string source)
        {
            _parser.TryParse(source, out var template, out var error);
            Assert.Null(template);
        }

        [Theory]
        [InlineData("{% assign var = 10 %}{% increment var %}{% increment var %}{{ var }}", "0110")]
        [InlineData("{% assign var = 10 %}{% decrement var %}{% decrement var %}{{ var }}", "0-110")]
        public Task IncrementDoesntAffectVariable(string source, string expected)
        {
            return CheckAsync(source, expected);
        }

        [Theory]
        [InlineData("{% increment %}{% increment %}{% increment %}", "012")]
        [InlineData("{% decrement %}{% decrement %}{% decrement %}", "0-1-2")]
        [InlineData("{% increment %}{% decrement %}{% increment %}", "0-10")]
        public Task IncrementCanBeUsedWithoutIdentifier(string source, string expected)
        {
            return CheckAsync(source, expected);
        }

        [Fact]
        public async Task ModelIsUsedAsFallback()
        {
            var source = "hello {{ firstname }} {{ lastname }}";
            var expected = "hello sebastien ros";

            _parser.TryParse(source, out var template, out var error);
            var context = new TemplateContext(new { lastname = "ros" });
            context.SetValue("firstname", "sebastien");

            var result = await template.RenderAsync(context);
            Assert.Equal(expected, result);
        }

        [Fact]
        public async Task NumbersAreFormattedUsingCulture()
        {
            var source = "{{ 1234.567 }}";
            var expectedFR = "1234,567";
            var expectedUS = "1234.567";

            _parser.TryParse(source, out var template, out var error);
            var options = new TemplateOptions();
            options.CultureInfo = new CultureInfo("en-US");
            var context = new TemplateContext(options);
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
            _parser.TryParse(source, out var templateFR, out var error);

            CultureInfo.CurrentCulture = new CultureInfo("en-US");
            _parser.TryParse(source, out var templateUS, out error);

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
            var source = "{% assign x = 'price' %}{% for p in products %}{{p[x]}}{% endfor %}";
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

        [Theory]
        [InlineData("{{ products | where: 'name' , 'product 2' }}", "{ name = product 2, price = 2 }")]
        [InlineData("{{ products | where: 'price' , 1 }}", "{ name = product 1, price = 1 }")]
        [InlineData(@"{% assign price = 3 %}{{ products | where: 'price' , price }}", "{ name = product 3, price = 3 }")]
        public Task ShouldProcessWhereFilter(string source, string expected)
        {
            return CheckAsync(source, expected, ctx => { ctx.SetValue("products", _products); });
        }


        [Fact]
        public async Task IncludeParamsShouldNotBeSetInTheParentTemplate()
        {
            var source = @"{% assign color = 'blue' %}
{% include 'Partials', color: 'red', shape: 'circle' %}

Parent Content
color: '{{ color }}'
shape: '{{ shape }}'";
            var expected = @"
Partial Content
Partials: ''
color: 'red'
shape: 'circle'

Parent Content
color: 'blue'
shape: ''";
            _parser.TryParse(source, out var template, out var error);

            var fileProvider = new MockFileProvider();
            fileProvider.Add("Partials.liquid", @"{{ 'Partial Content' }}
Partials: '{{ Partials }}'
color: '{{ color }}'
shape: '{{ shape }}'");

            var options = new TemplateOptions();
            options.FileProvider = fileProvider;
            var context = new TemplateContext(options);

            var result = await template.RenderAsync(context);

            Assert.Equal(expected, result);
        }

        [Fact]
        public async Task IncludeWithTagParamShouldNotBeSetInTheParentTemplate()
        {
            var source = @"{% assign Partials = 'parent value' %}
{% include 'Partials' with 'included value' %}

Parent Content
{{ Partials }}";
            var expected = @"
Partial Content
Partials: 'included value'
color: ''
shape: ''

Parent Content
parent value";

            var fileProvider = new MockFileProvider();
            fileProvider.Add("Partials.liquid", @"{{ 'Partial Content' }}
Partials: '{{ Partials }}'
color: '{{ color }}'
shape: '{{ shape }}'");

            _parser.TryParse(source, out var template, out var error);
            var options = new TemplateOptions();
            options.FileProvider = fileProvider;
            var context = new TemplateContext(options);

            var result = await template.RenderAsync(context);

            Assert.Equal(expected, result);
        }

        [Fact]
        public async Task ShouldEvaluateAsyncMember()
        {
            _parser.TryParse("{{ Content.Foo }}{{ Content.Baz }}", out var template, out var error);

            var options = new TemplateOptions();
            var context = new TemplateContext(options);
            context.SetValue("Content", new Content());
            options.MemberAccessStrategy.Register<Content, string>("Foo", async (obj, name) => { await Task.Delay(100); return "Bar"; });
            options.MemberAccessStrategy.Register<Content, string>(async (obj, name) => { await Task.Delay(100); return name; });

            var result = await template.RenderAsync(context);
            Assert.Equal("BarBaz", result);
        }

        [Fact]
        public async Task ShouldSetFactoryValue()
        {
            _parser.TryParse("{{ Test }}", out var template, out var error);
            bool set = false;
            var context = new TemplateContext();
            context.SetValue("Test", () => { set = true; return BooleanValue.True; });

            Assert.False(set);
            var result = await template.RenderAsync(context);
            Assert.Equal("true", result);
            Assert.True(set);
        }

        [Fact]
        public async Task ShouldLimitSteps()
        {
            _parser.TryParse("{% for w in (1..10000) %} FOO {% endfor %}", out var template, out var error);

            var options = new TemplateOptions();
            var context = new TemplateContext(options);
            options.MaxSteps = 100;

            await Assert.ThrowsAsync<InvalidOperationException>(() => template.RenderAsync(context).AsTask());
        }
        
        [Fact]
        public Task ForLoopLimitAndOffset()
        {
            var source = @"{% assign array = '1,2,3,4,5' | split: ',' %}{% for item in array limit:3 offset:2 %}{{ item }}{% endfor %}";
            var expected = "345";

            return CheckAsync(source, expected);
        }

        [Fact]
        public Task ForLoopLimitOnly()
        {
            var source = @"{% assign array = '1,2,3,4,5' | split: ',' %}{% for item in array limit:3 %}{{ item }}{% endfor %}";
            var expected = "123";

            return CheckAsync(source, expected);
        }

        [Fact]
        public Task ForLoopOffsetOnly()
        {
            var source = @"{% assign array = '1,2,3,4,5' | split: ',' %}{% for item in array offset:3 %}{{ item }}{% endfor %}";
            var expected = "45";

            return CheckAsync(source, expected);
        }

        [Fact]
        public async Task IgnoreCasing()
        {
            _parser.TryParse("{{ p.firsTname }}", out var template, out var error);

            var options = new TemplateOptions();
            options.MemberAccessStrategy.IgnoreCasing = true;
            options.MemberAccessStrategy.Register<Person>();

            var context = new TemplateContext(options);
            context.SetValue("p", new Person { Firstname = "John" });
            var result = await template.RenderAsync(context);
            Assert.Equal("John", result);

            options = new TemplateOptions();
            options.MemberAccessStrategy.IgnoreCasing = false;
            options.MemberAccessStrategy.Register<Person>();
            context = new TemplateContext(options);
            context.SetValue("p", new Person { Firstname = "John" });
            result = await template.RenderAsync(context);
            Assert.Equal("", result);
        }

        [Theory]
        [InlineData("{{ '5' | plus: 5 }}", "10")]
        [InlineData("{{ '5' | plus: 5.0 }}", "10.0")]
        [InlineData("{{ '5' | times: 5 }}", "25")]
        [InlineData("{{ '5' | minus: 5 }}", "0")]
        [InlineData("{{ '21' | divided_by: 3 }}", "7")]
        [InlineData("{{ '8888' | modulo: 10 }}", "8")]
        public Task ShouldConvertNumber(string source, string expected)
        {
            return CheckAsync(source, expected);
        }

        [Theory]
        [InlineData("{% if true %} { {%else%} } {% endif %}", " { ")]
        [InlineData("{% if true %}{ {%else%} } {% endif %}", "{ ")]
        [InlineData("{% if false %} { {%else%} } {% endif %}", " } ")]
        [InlineData("{% if false %} { {%else%}} {% endif %}", "} ")]
        [InlineData("{% if false %} { {%else%} }{% endif %}", " }")]
        [InlineData("{% if false %} { {%else%}}{% endif %}", "}")]
        public Task ShouldAcceptCurlyBracesInBlocks(string source, string expected)
        {
            return CheckAsync(source, expected);
        }

        [Theory]
        [InlineData("{% if true %} {{%else%} } {% endif %}")]
        public void ShouldNotAcceptInvalidCurlyBracesInBlocks(string source)
        {
            Assert.False(_parser.TryParse(source, out var _, out var _));
        }

        [Fact]
        public void ShouldParseNJsonSchema()
        {
            var source = @"
// <auto-generated>
//     Generated using the NSwag toolchain v{{ ToolchainVersion }} (http://NJsonSchema.org)
// </auto-generated>
//----------------------

{{ ExtensionCode.ImportCode }}

{% if HasModuleName -%}
module {{ ModuleName }} {
{% endif -%}
{% if HasNamespace -%}
namespace {{ Namespace }} {
{% endif -%}
{{ ExtensionCode.TopCode }}

{{ Types }}

{{ ExtensionCode.BottomCode }}

{% if HasNamespace -%}
}
{% endif -%}
{% if HasModuleName -%}
}
{% endif -%}
";

            Assert.True(_parser.TryParse(source, out var _, out var _));
        }

        [Theory]
        [InlineData("{% if value==1 %}{% endif %}")]
        [InlineData("{% if value<=1 %}{% endif %}")]
        [InlineData("{% if value>=1 %}{% endif %}")]
        [InlineData("{% if value<1 %}{% endif %}")]
        [InlineData("{% if value>1 %}{% endif %}")]
        [InlineData("{% if value contains 1 %}{% endif %}")]

        [InlineData("{% if value=='a' %}{% endif %}")]
        [InlineData("{% if value<='a' %}{% endif %}")]
        [InlineData("{% if value>='a' %}{% endif %}")]
        [InlineData("{% if value<'a' %}{% endif %}")]
        [InlineData("{% if value>'a' %}{% endif %}")]
        [InlineData("{% if value contains 'a' %}{% endif %}")]

        [InlineData("{% if value==a %}{% endif %}")]
        [InlineData("{% if value<=a %}{% endif %}")]
        [InlineData("{% if value>=a %}{% endif %}")]
        [InlineData("{% if value<a %}{% endif %}")]
        [InlineData("{% if value>a %}{% endif %}")]
        [InlineData("{% if value contains a %}{% endif %}")]

        [InlineData("{% if value== 1 %}{% endif %}")]
        [InlineData("{% if value<= 1 %}{% endif %}")]
        [InlineData("{% if value>= 1 %}{% endif %}")]
        [InlineData("{% if value< 1 %}{% endif %}")]
        [InlineData("{% if value> 1 %}{% endif %}")]

        [InlineData("{% if value==' a' %}{% endif %}")]
        [InlineData("{% if value<= 'a' %}{% endif %}")]
        [InlineData("{% if value>= 'a' %}{% endif %}")]
        [InlineData("{% if value< 'a' %}{% endif %}")]
        [InlineData("{% if value> 'a' %}{% endif %}")]

        [InlineData("{% if value== a %}{% endif %}")]
        [InlineData("{% if value<= a %}{% endif %}")]
        [InlineData("{% if value>= a %}{% endif %}")]
        [InlineData("{% if value< a %}{% endif %}")]
        [InlineData("{% if value> a %}{% endif %}")]
        public void ShouldParseValidOperators(string source)
        {
            Assert.True(_parser.TryParse(source, out var _, out var _));
        }

        [Theory]
        [InlineData("{% if value=1 %}{% endif %}")]
        [InlineData("{% if value===1 %}{% endif %}")]
        [InlineData("{% if value<<'1' %}{% endif %}")]
        [InlineData("{% if value<<<'1' %}{% endif %}")]
        [InlineData("{% if value<=='1' %}{% endif %}")]
        [InlineData("{% if value=>'1' %}{% endif %}")]
        [InlineData("{% if value contains'1' %}{% endif %}")] // an identifier operator needs a space after it
        public void ShouldNotParseInvalidOperators(string source)
        {
            Assert.False(_parser.TryParse(source, out var _, out var _));
        }

        [Fact]
        public async Task RawStatementShouldOutputSameText()
        {
            var source = @"
before
{% raw %}
{{ TEST 3 }}
{% endraw %}
after
";

            var expected = @"
before

{{ TEST 3 }}

after
";

            await CheckAsync(source, expected);
        }

        [Fact]
        public async Task DefaultMemberStrategyShouldSupportCamelCase()
        {
            var model = new { FirstName = "Sebastien" };
            var source = "{{ firstName }}";
            var expected = "Sebastien";

            _parser.TryParse(source, out var template, out var error);

            var options = new TemplateOptions();
            options.MemberAccessStrategy = new DefaultMemberAccessStrategy { MemberNameStrategy = MemberNameStrategies.CamelCase };
            var context = new TemplateContext(model, options);

            var result = await template.RenderAsync(context);
            Assert.Equal(expected, result);
        }

        [Fact]
        public async Task DefaultMemberStrategyShouldSupportSnakeCase()
        {
            var model = new { FirstName = "Sebastien" };
            var source = "{{ first_name }}";
            var expected = "Sebastien";

            _parser.TryParse(source, out var template, out var error);

            var options = new TemplateOptions();
            options.MemberAccessStrategy = new DefaultMemberAccessStrategy { MemberNameStrategy = MemberNameStrategies.SnakeCase };
            var context = new TemplateContext(model, options);

            var result = await template.RenderAsync(context);
            Assert.Equal(expected, result);
        }

        [Theory]
        [InlineData(true)]
        [InlineData(false)]
        public async Task UnsafeMemberStrategyShouldSupportCamelCase(bool registerModelType)
        {
            var model = new { FirstName = "Sebastien" };
            var source = "{{ firstName }}";
            var expected = "Sebastien";

            _parser.TryParse(source, out var template, out var error);

            var options = new TemplateOptions();
            options.MemberAccessStrategy = new UnsafeMemberAccessStrategy { MemberNameStrategy = MemberNameStrategies.CamelCase };
            var context = new TemplateContext(model, options, registerModelType);

            var result = await template.RenderAsync(context);
            Assert.Equal(expected, result);
        }

        [Theory]
        [InlineData(true)]
        [InlineData(false)]
        public async Task UnsafeMemberStrategyShouldSupportSnakeCase(bool registerModelType)
        {
            var model = new { FirstName = "Sebastien" };
            var source = "{{ first_name }}";
            var expected = "Sebastien";

            _parser.TryParse(source, out var template, out var error);

            var options = new TemplateOptions();
            options.MemberAccessStrategy = new UnsafeMemberAccessStrategy { MemberNameStrategy = MemberNameStrategies.SnakeCase };
            var context = new TemplateContext(model, options, registerModelType);

            var result = await template.RenderAsync(context);
            Assert.Equal(expected, result);
        }

        [Fact]
        public async Task ShouldIterateOnDictionaries()
        {
            var model = new
            {
                Capitals = new Dictionary<string, string> { { "France", "Paris" }, { "Spain", "Madrid" }, { "Italy", "Rome" } }
            };

            var source = "{% for i in Capitals %}{{ Capitals[i.first] }}{{ i[1] }}{% endfor %}";
            var expected = "ParisParisMadridMadridRomeRome";

            _parser.TryParse(source, out var template, out var error);

            var options = new TemplateOptions();
            options.MemberAccessStrategy = UnsafeMemberAccessStrategy.Instance;
            var context = new TemplateContext(model, options);

            var result = await template.RenderAsync(context);
            Assert.Equal(expected, result);
        }

        [Fact]
        public async Task ForVariableShouldNotAlterContext()
        {
            var source = @"
                {%- assign c = '0' -%}
                {%- for c in (1..3) -%}{{ c }}{%- assign c = 4 -%}{% endfor -%}
                {{- c -}}
            ";

            _parser.TryParse(source, out var template, out var error);
            var context = new TemplateContext();
            var result = await template.RenderAsync(context);
            Assert.Equal("1234", result);
        }

        [Fact]
        public async Task ForStringValueDoesntEnumerate()
        {
            var source = @"
                {%- assign x = '123' -%}
                {%- for c in x -%}{{ c }}{{ c }}{% endfor -%}
                {{- c -}}
            ";

            _parser.TryParse(source, out var template, out var error);
            var context = new TemplateContext();
            var result = await template.RenderAsync(context);
            Assert.Equal("123123", result);
        }
    }
}
