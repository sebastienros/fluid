using System;
using System.Collections.Generic;
using System.Globalization;
using System.Reflection;
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

        private static readonly TimeZoneInfo Eastern = TimeZoneConverter.TZConvert.GetTimeZoneInfo("America/New_York");
        private static readonly TimeZoneInfo Paris = TimeZoneConverter.TZConvert.GetTimeZoneInfo("Europe/Paris");

        private object _products = new[]
        {
            new { name = "product 1", price = 1 },
            new { name = "product 2", price = 2 },
            new { name = "product 3", price = 3 },
        };

        private async Task CheckAsync(string source, string expected, Action<TemplateContext> init = null)
        {
            Assert.True(_parser.TryParse(source, out var template, out var error));

            var context = new TemplateContext();
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
                case "html": encoder = HtmlEncoder.Default; break;
                case "url": encoder = UrlEncoder.Default; break;
                case "null": encoder = NullEncoder.Default; break;
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
            var result = await template.RenderAsync(new TemplateContext { Captured = (identifier, captured, context) => new StringValue(captured.ToStringValue().ToUpper(), false) }, HtmlEncoder.Default);
            Assert.Equal("HELLO <BR /> WORLD", result);
        }

        [Fact]
        public async Task ShouldNotDoubleEncodeRawCaptureWithEscapeFilter()
        {
            // Issue: Capturing raw liquid tags and rendering with escape filter should not double-encode
            var source = @"{% capture r %}
{% raw %}
{% assign cultures = Culture | supported_cultures %}
<ul>item</ul>
{% endraw %}
{% endcapture %}
{{ r | escape }}";
            
            _parser.TryParse(source, out var template, out var error);
            var result = await template.RenderAsync(new TemplateContext(), HtmlEncoder.Default);
            
            // The raw content should be escaped once, not double-encoded
            Assert.Contains("{% assign cultures = Culture | supported_cultures %}", result);
            Assert.Contains("&lt;ul&gt;item&lt;/ul&gt;", result);
            Assert.DoesNotContain("&amp;lt;", result); // Should not be double-encoded
            Assert.DoesNotContain("&amp;gt;", result); // Should not be double-encoded
        }

        [Fact]
        public async Task ShouldNotEncodeRawCaptureWithoutEscapeFilter()
        {
            // Capturing raw liquid tags without escape filter should output unencoded
            var source = @"{% capture r %}
{% raw %}
{% assign cultures = Culture | supported_cultures %}
<ul>item</ul>
{% endraw %}
{% endcapture %}
{{ r }}";
            
            _parser.TryParse(source, out var template, out var error);
            var result = await template.RenderAsync(new TemplateContext(), HtmlEncoder.Default);
            
            // The raw content should not be encoded
            Assert.Contains("{% assign cultures = Culture | supported_cultures %}", result);
            Assert.Contains("<ul>item</ul>", result);
            Assert.DoesNotContain("&lt;", result);
            Assert.DoesNotContain("&gt;", result);
        }

        [Fact]
        public async Task EscapeFilterShouldNotDoubleEncode()
        {
            // Using escape filter with HtmlEncoder should not double-encode
            var source = @"{{ '<div>test</div>' | escape }}";
            
            _parser.TryParse(source, out var template, out var error);
            var result = await template.RenderAsync(new TemplateContext(), HtmlEncoder.Default);
            
            // Should be encoded once
            Assert.Equal("&lt;div&gt;test&lt;/div&gt;", result);
            Assert.DoesNotContain("&amp;", result); // Should not be double-encoded
        }

        [Fact]
        public async Task EscapeOnceFilterShouldNotDoubleEncode()
        {
            // Using escape_once filter with HtmlEncoder should not double-encode
            var source = @"{{ '&lt;div&gt;test&lt;/div&gt;' | escape_once }}";
            
            _parser.TryParse(source, out var template, out var error);
            var result = await template.RenderAsync(new TemplateContext(), HtmlEncoder.Default);
            
            // Should be encoded once (escape_once should decode then encode)
            Assert.Equal("&lt;div&gt;test&lt;/div&gt;", result);
            Assert.DoesNotContain("&amp;", result); // Should not be double-encoded
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
        public async Task ShouldRenderNullValueFromContext()
        {
            _parser.TryParse("{{ x }}", out var template, out var error);
            var context = new TemplateContext();
            context.SetValue("x", (object)null);

            var result = await template.RenderAsync(context);
            Assert.Equal(string.Empty, result);
        }

        [Fact]
        public async Task ShouldRenderNullValueFromProperty()
        {
            _parser.TryParse("{{ c.Value }}", out var template, out var error);

            var options = new TemplateOptions();

            var context = new TemplateContext(options);
            context.SetValue("c", new NullStringContainer());

            var result = await template.RenderAsync(context);
            Assert.Equal(string.Empty, result);
        }

        [Fact]
        public async Task ShouldRenderNullValueFromToString()
        {
            _parser.TryParse("{{ c }}", out var template, out var error);

            var options = new TemplateOptions();

            var context = new TemplateContext(options);
            context.SetValue("c", new NullStringContainer());

            var result = await template.RenderAsync(context);
            Assert.Equal(string.Empty, result);
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
        public async Task ShouldEvaluateDateTimeValue()
        {
            // DateTimeValue is rendered as Universal Sortable Date/Time (u)
            _parser.TryParse("{{ x }}", out var template, out var error);
            var context = new TemplateContext { TimeZone = Eastern };
            context.SetValue("x", new DateTime(2022, 10, 20, 17, 00, 00, 000, DateTimeKind.Utc));

            var result = await template.RenderAsync(context);
            Assert.Equal("2022-10-20 17:00:00Z", result);
        }

        [Fact]
        public async Task ShouldEvaluateTimeSpanValue()
        {
            // TimeSpan should be converted to DateTimeValue
            // Then a DateTimeValue is rendered as Universal Sortable Date/Time (u)

            _parser.TryParse("{{ x }}", out var template, out var _);
            var context = new TemplateContext { TimeZone = Eastern };
            var oneHour = new TimeSpan(0, 1, 00, 00, 000);
            context.SetValue("x", oneHour);

            var result = await template.RenderAsync(context);
            Assert.Equal("1970-01-01 01:00:00Z", result);
        }

        [Fact]
        public async Task ShouldHandleDateTimeMinValueWithPositiveTimezoneOffset()
        {
            // Set a timezone offset of +2 hours (like EET - Eastern European Time)
            var plusTwoTimezone = TimeZoneInfo.CreateCustomTimeZone("Custom+2", TimeSpan.FromHours(2), "UTC+2", "UTC+2");
            
            _parser.TryParse("{{ foo }} {{ date }}", out var template, out var error);
            
            var context = new TemplateContext { TimeZone = plusTwoTimezone };
            context.SetValue("foo", "bar");
            context.SetValue("date", DateTime.MinValue);

            // This should not throw ArgumentOutOfRangeException
            var result = await template.RenderAsync(context);
            
            // DateTime.MinValue should be rendered as the minimum DateTimeOffset value
            Assert.Contains("bar", result);
            Assert.Contains("0001-01-01", result);
        }

        [Fact]
        public async Task ShouldHandleDateTimeNearMinValueWithPositiveTimezoneOffset()
        {
            // Set a timezone offset of +2 hours (like EET - Eastern European Time)
            var plusTwoTimezone = TimeZoneInfo.CreateCustomTimeZone("Custom+2", TimeSpan.FromHours(2), "UTC+2", "UTC+2");
            
            _parser.TryParse("{{ foo }} {{ date }}", out var template, out var error);
            
            var context = new TemplateContext { TimeZone = plusTwoTimezone };
            context.SetValue("foo", "bar");
            context.SetValue("date", DateTime.MinValue.AddHours(1));

            // This should not throw ArgumentOutOfRangeException even with DateTime.MinValue + 1 hour
            var result = await template.RenderAsync(context);
            
            // DateTime near MinValue should be rendered as the minimum DateTimeOffset value
            Assert.Contains("bar", result);
            Assert.Contains("0001-01-01", result);
        }

        [Fact]
        public async Task ShouldHandleDateTimeMaxValueWithNegativeTimezoneOffset()
        {
            // Set a timezone offset of -2 hours (like Brazil Standard Time)
            var minusTwoTimezone = TimeZoneInfo.CreateCustomTimeZone("Custom-2", TimeSpan.FromHours(-2), "UTC-2", "UTC-2");
            
            _parser.TryParse("{{ foo }} {{ date }}", out var template, out var error);
            
            var context = new TemplateContext { TimeZone = minusTwoTimezone };
            context.SetValue("foo", "bar");
            context.SetValue("date", DateTime.MaxValue);

            // This should not throw ArgumentOutOfRangeException
            var result = await template.RenderAsync(context);
            
            // DateTime.MaxValue should be rendered as the maximum DateTimeOffset value
            Assert.Contains("bar", result);
            Assert.Contains("9999-12-31", result);
        }

        [Fact]
        public async Task ShouldHandleDateTimeNearMaxValueWithNegativeTimezoneOffset()
        {
            // Set a timezone offset of -2 hours (like Brazil Standard Time)
            var minusTwoTimezone = TimeZoneInfo.CreateCustomTimeZone("Custom-2", TimeSpan.FromHours(-2), "UTC-2", "UTC-2");
            
            _parser.TryParse("{{ foo }} {{ date }}", out var template, out var error);
            
            var context = new TemplateContext { TimeZone = minusTwoTimezone };
            context.SetValue("foo", "bar");
            context.SetValue("date", DateTime.MaxValue.AddHours(-1));

            // This should not throw ArgumentOutOfRangeException even with DateTime.MaxValue - 1 hour
            var result = await template.RenderAsync(context);
            
            // DateTime near MaxValue should be rendered as the maximum DateTimeOffset value
            Assert.Contains("bar", result);
            Assert.Contains("9999-12-31", result);
        }

        [Fact]
        public async Task ShouldEvaluateObjectProperty()
        {
            _parser.TryParse("{{ p.Firstname }}", out var template, out var error);

            var options = new TemplateOptions();

            var context = new TemplateContext(options);
            context.SetValue("p", new Person { Firstname = "John" });

            var result = await template.RenderAsync(context);
            Assert.Equal("John", result);
        }

        [Fact]
        public async Task ShouldEvaluateObjectPropertyWhenInterfaceRegisteredAsGlobal()
        {
            var options = new TemplateOptions();

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
        public async Task ShouldAllowInterfaceMembers()
        {
            var options = new TemplateOptions();

            _parser.TryParse("{{ p.Name }}", out var template, out var error);

            var context = new TemplateContext(options);
            context.SetValue("p", new Dog { Name = "Rex" });

            var result = await template.RenderAsync(context);
            Assert.Equal("Rex", result);
        }

        [Fact]
        public async Task ShouldEvaluateInheritedObjectProperty()
        {
            _parser.TryParse("{{ e.Firstname }} {{ e.Salary }}", out var template, out var error);

            var options = new TemplateOptions();
            var context = new TemplateContext(options);
            context.SetValue("e", new Employee { Firstname = "John", Salary = 550 });

            var result = await template.RenderAsync(context);
            Assert.Equal("John 550", result);
        }

        [Fact]
        public async Task ShouldAllowInheritedMember()
        {
            // The Employee class is not registered, hence any access to its properties should return nothing
            // but the Person class is registered, so Name should be available
            _parser.TryParse("{{ c.Director.Firstname }} {{ c.Director.Salary }}", out var template, out var error);

            var options = new TemplateOptions();
            var context = new TemplateContext(options);
            context.SetValue("c", new Company { Director = new Employee { Firstname = "John", Salary = 550 } });

            var result = await template.RenderAsync(context);
            Assert.Equal("John 550", result);
        }

        [Fact]
        public async Task StringIndexerReturnsNil()
        {
            _parser.TryParse("{% if x[0] == blank %}true{% else %}false{% endif %}", out var template, out var error);
            var context = new TemplateContext();
            context.SetValue("x", "abc");

            var result = await template.RenderAsync(context);
            Assert.Equal("true", result);
        }

        [Fact]
        public async Task ShouldEvaluateCustomObjectIndex()
        {
            var options = new TemplateOptions();
            options.ValueConverters.Add(o => o is Person p ? new PersonValue(p) : null);

            var context = new TemplateContext(options);
            context.SetValue("p", new Person { Firstname = "Bill" });

            _parser.TryParse("{{ p[1] }} {{ p['blah'] }}", out var template, out var error);
            var result = await template.RenderAsync(context);
            Assert.Equal("Bill 1 Bill blah", result);
        }

        private sealed class NullStringContainer
        {
            public string Value => null;

            public override string ToString() => null;
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
        [InlineData(@"{%cycle 'a', 'b'%}{%cycle foo: 'a', 'b'%}", "aa")]
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
        [InlineData("{% assign var = 10 %}{% decrement var %}{% decrement var %}{{ var }}", "-1-210")]
        public Task IncrementDoesntAffectVariable(string source, string expected)
        {
            return CheckAsync(source, expected);
        }

        [Theory]
        [InlineData("{% increment %}{% increment %}{% increment %}", "012")]
        [InlineData("{% decrement %}{% decrement %}{% decrement %}", "-1-2-3")]
        [InlineData("{% increment %}{% decrement %}{% increment %}", "000")]
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

        [Theory]
        [InlineData("{{ dic[1] }}", "/1/")]
        [InlineData("{{ dic['1'] }}", "/1/")]
        [InlineData("{{ dic[10] }}", "/10/")]
        [InlineData("{{ dic['10'] }}", "/10/")]
        [InlineData("{{ dic.2_ }}", "/2_/")] // Note: dic.10 is not valid per Shopify Liquid standard, use bracket notation
        public Task PropertiesCanBeDigits(string source, string expected)
        {
            return CheckAsync(source, expected, ctx =>
            {
                ctx.SetValue("dic", new Dictionary<string, string> { { "1", "/1/" }, { "2_", "/2_/" }, { "10", "/10/" } });
            });
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

            // Options are inherited from TemplateOptions
            var options = new TemplateOptions
            {
                MaxSteps = 100
            };

            var context = new TemplateContext(options);

            await Assert.ThrowsAsync<InvalidOperationException>(() => template.RenderAsync(context).AsTask());

            // Options are customized on TemplateContext
            context.MaxSteps = 0;

            await template.RenderAsync(context).AsTask();
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
            _parser.TryParse("{{ p.firsTname }}", out var template, out var _);

            var options = new TemplateOptions() { ModelNamesComparer = StringComparer.OrdinalIgnoreCase };
            var context = new TemplateContext(options);
            context.SetValue("p", new Person { Firstname = "John" });
            var result = await template.RenderAsync(context);
            Assert.Equal("John", result);

            options = new TemplateOptions() { ModelNamesComparer = StringComparer.Ordinal };
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
        public void DictionaryShouldWorkWithComparers_SnakeCase()
        {
            var comparer = StringComparers.SnakeCase;
            var dict = new Dictionary<string, string>(comparer);
            dict["FirstName"] = "Sebastien";
            Assert.True(dict.ContainsKey("first_name"));
            Assert.Equal("Sebastien", dict["first_name"]);
        }
        
        [Fact]
        public async Task DefaultMemberStrategyShouldSupportSnakeCase()
        {
            var model = new { FirstName = "Sebastien" };
            var source = "{{ first_name }} {{ last_name }}";

            _parser.TryParse(source, out var template, out var error);

            var options = new TemplateOptions() { ModelNamesComparer = StringComparers.SnakeCase };
            var context = new TemplateContext(model, options);
            context.SetValue("LastName", "Ros");

            var result = await template.RenderAsync(context);
            Assert.Equal("Sebastien Ros", result);
        }

        [Fact]
        public async Task DefaultMemberStrategyShouldSupportCamelCase()
        {
            var model = new { FirstName = "Sebastien" };
            var source = "{{ firstName }} {{ lastName}}";

            _parser.TryParse(source, out var template, out var error);

            var options = new TemplateOptions() { ModelNamesComparer = StringComparers.CamelCase };
            var context = new TemplateContext(model, options);
            context.SetValue("LastName", "Ros");

            var result = await template.RenderAsync(context);
            Assert.Equal("Sebastien Ros", result);
        }

        [Fact]
        public async Task DefaultMemberStrategyShouldSupportAnyCase()
        {
            var model = new { FirstName = "Sebastien" };
            var source = "{{ fIrSTnAme }} {{ lAsTnAme}}";

            _parser.TryParse(source, out var template, out var error);

            var options = new TemplateOptions() { ModelNamesComparer = StringComparer.OrdinalIgnoreCase };
            var context = new TemplateContext(model, options);
            context.SetValue("LastName", "Ros");

            var result = await template.RenderAsync(context);
            Assert.Equal("Sebastien Ros", result);
        }

        [Fact]
        public void SnakeCaseHandlesAcronymsCorrectly()
        {
            // Test UserName -> user_name
            Assert.True(StringComparers.SnakeCase.Equals("UserName", "user_name"));
            
            // Test OpenAIModel -> open_ai_model
            Assert.True(StringComparers.SnakeCase.Equals("OpenAIModel", "open_ai_model"));

            // Test OEMVendor -> oem_vendor
            Assert.True(StringComparers.SnakeCase.Equals("OEMVendor", "oem_vendor"));

            // Test IDSecurity -> id_security
            Assert.True(StringComparers.SnakeCase.Equals("IDSecurity", "id_security"));

            // Test ID -> id
            Assert.True(StringComparers.SnakeCase.Equals("ID", "id"));

            // Test XMLParser -> xml_parser
            Assert.True(StringComparers.SnakeCase.Equals("XMLParser", "xml_parser"));

            // Test HTMLElement -> html_element
            Assert.True(StringComparers.SnakeCase.Equals("HTMLElement", "html_element"));

            // Test IOError -> io_error
            Assert.True(StringComparers.SnakeCase.Equals("IOError", "io_error"));

            // Test JSONData -> json_data
            Assert.True(StringComparers.SnakeCase.Equals("JSONData", "json_data"));
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

        [Fact]
        public async Task ArraysShouldCompareElements()
        {
            var source = """
                {% assign people1 = "alice, bob, carol" | split: ", " %}
                {% assign people2 = "alice, bob, carol" | split: ", " %}

                {% if people1 == people2 %}true{%else%}false{% endif %}
            """;

            _parser.TryParse(source, out var template);
            var context = new TemplateContext();
            var result = await template.RenderAsync(context);
            Assert.Contains("true", result);
        }

        [Fact]
        public async Task InlineCommentShouldNotRender()
        {
            var source = "Hello {% # this is a comment %} World";
            await CheckAsync(source, "Hello  World");
        }

        [Fact]
        public async Task InlineCommentShouldNotRenderAnyContent()
        {
            var source = "{% # this is a comment with text %}Result";
            await CheckAsync(source, "Result");
        }

        [Fact]
        public async Task InlineCommentShouldWorkWithWhitespaceTrim()
        {
            var source = "Hello{%- # this is a comment -%}World";
            await CheckAsync(source, "HelloWorld");
        }

        [Fact]
        public async Task InlineCommentShouldWorkInTemplates()
        {
            var source = @"
                {% # Start of template %}
                {% assign name = 'John' %}
                {% # Output the name %}
                Hello {{ name }}!
                {% # End of template %}
            ";
            
            _parser.TryParse(source, out var template, out var error);
            var context = new TemplateContext();
            var result = await template.RenderAsync(context);
            Assert.Contains("Hello John!", result);
            Assert.DoesNotContain("Start of template", result);
            Assert.DoesNotContain("Output the name", result);
            Assert.DoesNotContain("End of template", result);
        }

        [Fact]
        public async Task InlineCommentShouldWorkBetweenTags()
        {
            var source = @"
                {% if true %}
                {% # This is between if tags %}
                Success
                {% endif %}
            ";
            
            _parser.TryParse(source, out var template, out var error);
            var context = new TemplateContext();
            var result = await template.RenderAsync(context);
            Assert.Contains("Success", result);
            Assert.DoesNotContain("This is between if tags", result);
        }

        [Fact]
        public async Task NullPropertyEvaluatesToFalse()
        {
            var source = @"
                {% if a %}a is true{% else %}a is false{% endif %}
                {% if b %}b is true{% else %}b is false{% endif %}
                ";

            _parser.TryParse(source, out var template, out var error);
            var context = new TemplateContext(new { a = (string)null, b = "" });
            var result = await template.RenderAsync(context);
            Assert.Contains("a is false", result);
            Assert.Contains("b is true", result);
        }
    }
}
