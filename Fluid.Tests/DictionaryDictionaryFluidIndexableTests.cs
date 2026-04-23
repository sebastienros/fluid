using Fluid.Values;
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
}
