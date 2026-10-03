using Fluid.Values;
using System.Globalization;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using Xunit;

namespace Fluid.Tests;

public class DictionaryDictionaryFluidIndexableTests
{
#if COMPILED
    private static readonly FluidParser _parser = new FluidParser().Compile();
#else
    private static readonly FluidParser _parser = new FluidParser();
#endif

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

        var value = FluidValue.Create(items, new TemplateOptions());

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

        var value = FluidValue.Create(items, new TemplateOptions());

        var castedValue = value.ToObjectValue() as IFluidIndexable;

        Assert.NotNull(castedValue);

        Assert.Equal(items.Count, castedValue.Keys.Count());
    }

    [Fact]
    public void TemplateShouldRenderNumericDictionaryKeys()
    {
        var inventoryByWarehouse = new Dictionary<long, string>()
        {
            {101, "12"},
            {205, "8"},
        };
        var template = _parser.Parse(
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
        var options = new TemplateOptions { CultureInfo = CultureInfo.GetCultureInfo("fr-FR") };
        var template = _parser.Parse(
            "{% for entry in items %}{{ entry[0] }}={{ entry[1] }};{% endfor %}");
        var context = new TemplateContext(options);
        context.SetValue("items", items);

        var result = template.Render(context);

        Assert.Equal("1,5=value;", result);
    }

    [Fact]
    public void TemplateShouldLookUpNumericDictionaryKeysByString()
    {
        var context = new TemplateContext();
        context.SetValue("items", new Dictionary<long, string> { { 101, "value" } });
        var template = _parser.Parse("{{ items['101'] }}");

        Assert.Equal("value", template.Render(context));
    }

    [Fact]
    public void LookupShouldPreferAnExactStringKey()
    {
        var items = new Hashtable { [1] = "numeric", ["1"] = "string" };
        var indexable = new DictionaryDictionaryFluidIndexable(items, new TemplateOptions());

        Assert.True(indexable.TryGetValue("1", out var value));
        Assert.Equal("string", value.ToStringValue());
    }

    [Fact]
    public void MissingKeyShouldReturnNil()
    {
        var items = new Hashtable { [1] = "numeric", ["one"] = "string" };
        var indexable = new DictionaryDictionaryFluidIndexable(items, new TemplateOptions());

        Assert.False(indexable.TryGetValue("missing", out var value));
        Assert.Same(NilValue.Instance, value);
    }
}
