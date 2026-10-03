using Fluid.Values;
using System.Globalization;
using System.Collections.Generic;
using System.Linq;
using Xunit;

namespace Fluid.Tests;

public class DictionaryDictionaryFluidIndexableTests
{
    [Fact]
    public void CountShouldHaveTheSameLengthAsKeysWhenKeyInLong()
    {
        var items = new Dictionary<long, string>()
        {
            {1, "a"},
            {2, "b"},
            {3, "c"},
            {4, "d"},
            {5, "e"},
        };

        var value = FluidValue.Create(items, new TemplateOptionsBuilder().Build());

        var castedValue = value.ToObjectValue() as IFluidIndexable;

        Assert.NotNull(castedValue);

        Assert.Equal(items.Count, castedValue.Keys.Count());
    }

    [Fact]
    public void CountShouldHaveTheSameLengthAsKeysWhenKeyInString()
    {
        var items = new Dictionary<string, string>()
        {
            {"1", "a"},
            {"2", "b"},
            {"3", "c"},
            {"4", "d"},
            {"5", "e"},
        };

        var value = FluidValue.Create(items, new TemplateOptionsBuilder().Build());

        var castedValue = value.ToObjectValue() as IFluidIndexable;

        Assert.NotNull(castedValue);

        Assert.Equal(items.Count, castedValue.Keys.Count());
    }

    [Fact]
    public void ToNumberValueShouldReturnDictionaryItemCount()
    {
        var items = new Dictionary<string, string>()
        {
            {"1", "a"},
            {"2", "b"},
            {"3", "c"},
        };

        var value = FluidValue.Create(items, new TemplateOptionsBuilder().Build());

        Assert.Equal(items.Count, value.ToNumberValue());
    }

    [Fact]
    public void ToNumberValueShouldReturnZeroForEmptyDictionary()
    {
        var value = FluidValue.Create(new Dictionary<string, string>(), new TemplateOptionsBuilder().Build());

        Assert.Equal(0, value.ToNumberValue());
    }

    [Fact]
    public void TemplateShouldRenderNumericDictionaryKeys()
    {
        var inventoryByWarehouse = new Dictionary<long, string>()
        {
            {101, "12"},
            {205, "8"},
        };
        var template = new FluidParser().Parse(
            "{% for entry in inventoryByWarehouse %}{{ entry[0] }}={{ entry[1] }};{% endfor %}");
        var context = new TemplateContext();
        context.SetValue("inventoryByWarehouse", inventoryByWarehouse);

        var result = template.Render(context);

        Assert.Equal("101=12;205=8;", result);
    }

    [Fact]
    public void TemplateShouldFormatNumericDictionaryKeysUsingConfiguredCulture()
    {
        var items = new Dictionary<decimal, string>()
        {
            {1.5m, "value"},
        };
        var options = new TemplateOptionsBuilder().WithCultureInfo(CultureInfo.GetCultureInfo("fr-FR")).Build();
        var template = new FluidParser().Parse(
            "{% for entry in items %}{{ entry[0] }}={{ entry[1] }};{% endfor %}");
        var context = new TemplateContext(options);
        context.SetValue("items", items);

        var result = template.Render(context);

        Assert.Equal("1,5=value;", result);
    }
}
