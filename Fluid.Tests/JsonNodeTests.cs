using System.Text.Json.Nodes;
using System.Threading.Tasks;
using Fluid.Values;
using Xunit;

namespace Fluid.Tests
{
    public class JsonNodeTests
    {
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

            var parser = new FluidParser();
            var template = parser.Parse(
                "{% assign total_price = 0 %}" +
                "{% for guarantee in RiscObject[0].GaranteeObject %}" +
                "{% assign total_price = total_price | plus: guarantee.Price %}" +
                "{% endfor %}{{ total_price }}");

            Assert.Equal("30.75", await template.RenderAsync(new TemplateContext(model)));
        }

        [Fact]
        public void CustomValueConvertersTakePrecedenceForJsonValues()
        {
            var options = new TemplateOptionsBuilder().AddValueConverter(value => value is JsonValue ? "custom" : null).Build();

            var value = FluidValue.Create(JsonValue.Create(42), options);

            Assert.Equal("custom", value.ToStringValue());
        }

        [Fact]
        public void JsonValuesCreatedFromClrScalarsAreConverted()
        {
            var options = new TemplateOptionsBuilder().Build();

            Assert.Equal(42, FluidValue.Create(JsonValue.Create(42), options).ToNumberValue());
            Assert.Equal("hello", FluidValue.Create(JsonValue.Create("hello"), options).ToStringValue());
            Assert.True(FluidValue.Create(JsonValue.Create(true), options).ToBooleanValue());
        }
    }
}
