using System;
using System.Globalization;
using System.Text.Json.Nodes;
using System.Threading.Tasks;
using Fluid.Values;
using Xunit;

namespace Fluid.Tests
{
    public class JsonNodeTests
    {
#if COMPILED
        private static readonly FluidParser _parser = new FluidParser().Compile();
#else
        private static readonly FluidParser _parser = new FluidParser();
#endif

        [Fact]
        public async Task JsonObjectModelSupportsCalculations()
        {
            var model = JsonNode.Parse("""
                {
                  "RiscObject": [
                    {
                      "GaranteeObject": [
                        { "Price": 10.25 },
                        { "Price": 20.5 }
                      ]
                    }
                  ]
                }
                """).AsObject();

            var template = _parser.Parse(
                "{% assign total_price = 0 %}" +
                "{% for guarantee in RiscObject[0].GaranteeObject %}" +
                "{% assign total_price = total_price | plus: guarantee.Price %}" +
                "{% endfor %}{{ total_price }}");

            Assert.Equal("30.75", await template.RenderAsync(new TemplateContext(model)));
        }

        [Theory]
        [InlineData("2020-05-18T02:13:09+00:00", "date: '%Y-%m-%d %H:%M:%S %z'", "2020-05-18 02:13:09 +0000")]
        [InlineData("2020-05-18T02:13:09+00:00", "format_date: 'yyyy-MM-dd HH:mm:ss zzz'", "2020-05-18 02:13:09 +00:00")]
        [InlineData("2020-05-18T02:13:09+00:00", "time_zone: 'local' | format_date: 'yyyy-MM-dd HH:mm:ss zzz'", "2020-05-18 04:13:09 +02:00")]
        [InlineData("2020-05-18T02:13:09", "date: '%Y-%m-%d %H:%M:%S %z'", "2020-05-18 02:13:09 +0200")]
        [InlineData("2020-05-18T02:13:09", "format_date: 'yyyy-MM-dd HH:mm:ss zzz'", "2020-05-18 02:13:09 +02:00")]
        [InlineData("2020-05-18T02:13:09", "time_zone: 'local' | format_date: 'yyyy-MM-dd HH:mm:ss zzz'", "2020-05-18 02:13:09 +02:00")]
        [InlineData("now", "date: '%Y-%m-%d %H:%M:%S %z'", "2020-05-18 02:13:09 +0000")]
        [InlineData("now", "format_date: 'yyyy-MM-dd HH:mm:ss zzz'", "2020-05-18 02:13:09 +00:00")]
        [InlineData("now", "time_zone: 'local' | format_date: 'yyyy-MM-dd HH:mm:ss zzz'", "2020-05-18 04:13:09 +02:00")]
        [InlineData("today", "date: '%Y-%m-%d %H:%M:%S %z'", "2020-05-18 02:13:09 +0000")]
        [InlineData("today", "format_date: 'yyyy-MM-dd HH:mm:ss zzz'", "2020-05-18 02:13:09 +00:00")]
        [InlineData("today", "time_zone: 'local' | format_date: 'yyyy-MM-dd HH:mm:ss zzz'", "2020-05-18 04:13:09 +02:00")]
        [InlineData(0, "date: '%Y-%m-%d %H:%M:%S %z'", "1970-01-01 02:00:00 +0200")]
        [InlineData(0, "format_date: 'yyyy-MM-dd HH:mm:ss zzz'", "1970-01-01 02:00:00 +02:00")]
        [InlineData(0, "time_zone: 'local' | format_date: 'yyyy-MM-dd HH:mm:ss zzz'", "1970-01-01 02:00:00 +02:00")]
        [InlineData(0.5, "date: '%Y-%m-%d %H:%M:%S.%L %z'", "1970-01-01 02:00:00.500 +0200")]
        [InlineData(0.5, "format_date: 'yyyy-MM-dd HH:mm:ss.fff zzz'", "1970-01-01 02:00:00.500 +02:00")]
        [InlineData(0.5, "time_zone: 'local' | format_date: 'yyyy-MM-dd HH:mm:ss.fff zzz'", "1970-01-01 02:00:00.500 +02:00")]
        [InlineData("not a date", "date: '%Y'", "not a date")]
        [InlineData("not a date", "format_date: 'yyyy'", "")]
        [InlineData("not a date", "time_zone: 'local'", "")]
        [InlineData(null, "date: '%Y'", "")]
        [InlineData(null, "format_date: 'yyyy'", "")]
        [InlineData(null, "time_zone: 'local'", "")]
        public async Task JsonObjectModelSupportsDateFilters(object input, string filters, string expected)
        {
            var options = new TemplateOptions
            {
                CultureInfo = CultureInfo.InvariantCulture,
                TimeZone = TimeZoneInfo.CreateCustomTimeZone("Fixed", TimeSpan.FromHours(2), "Fixed", "Fixed"),
                Now = () => new DateTimeOffset(2020, 5, 18, 2, 13, 9, TimeSpan.Zero)
            };
            var model = new JsonObject
            {
                ["nested"] = new JsonObject
                {
                    ["published"] = JsonValue.Create(input)
                }
            };
            var template = _parser.Parse("{{ nested.published | " + filters + " }}");

            Assert.Equal(expected, await template.RenderAsync(new TemplateContext(model, options)));
            Assert.Equal(expected, await template.RenderAsync(new TemplateContext(JsonNode.Parse(model.ToJsonString()).AsObject(), options)));
        }

        [Fact]
        public void CustomValueConvertersTakePrecedenceForJsonValues()
        {
            var options = new TemplateOptions();
            options.ValueConverters.Add(value => value is JsonValue ? "custom" : null);

            var value = FluidValue.Create(JsonValue.Create(42), options);

            Assert.Equal("custom", value.ToStringValue());
        }

        [Fact]
        public void JsonValuesCreatedFromClrScalarsAreConverted()
        {
            var options = new TemplateOptions();

            Assert.Equal(42, FluidValue.Create(JsonValue.Create(42), options).ToNumberValue());
            Assert.Equal("hello", FluidValue.Create(JsonValue.Create("hello"), options).ToStringValue());
            Assert.Equal("hello", FluidValue.Create(JsonValue.Create<object>("hello"), options).ToStringValue());
            Assert.True(FluidValue.Create(JsonValue.Create(true), options).ToBooleanValue());
        }
    }
}
