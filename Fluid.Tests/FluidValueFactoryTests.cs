using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Reflection;
using System.Text.Encodings.Web;
using System.Threading.Tasks;
using Fluid.Values;
using Xunit;

namespace Fluid.Tests;

public class FluidValueFactoryTests
{
    [Fact]
    public void BuiltInConcreteValuesShouldHaveOnlyPrivateConstructors()
    {
        var types = typeof(FluidValue).Assembly.GetTypes()
            .Where(type => !type.IsAbstract && typeof(FluidValue).IsAssignableFrom(type))
            .ToArray();

        Assert.NotEmpty(types);
        foreach (var type in types)
        {
            Assert.Empty(type.GetConstructors());
            Assert.All(type.GetConstructors(BindingFlags.Instance | BindingFlags.NonPublic),
                constructor => Assert.True(constructor.IsPrivate, type.FullName));
        }
    }

    [Fact]
    public void EmptyArraysShouldReuseSingleton()
    {
        Assert.Same(ArrayValue.Empty, ArrayValue.Create(null));
        Assert.Same(ArrayValue.Empty, ArrayValue.Create(Array.Empty<FluidValue>()));
        Assert.Same(ArrayValue.Empty, ArrayValue.Create([]));
        Assert.Same(ArrayValue.Empty, FluidValue.Create(Array.Empty<FluidValue>(), TemplateOptions.Default));
        Assert.Same(ArrayValue.Empty, FluidValue.Create(Enumerable.Empty<FluidValue>(), TemplateOptions.Default));
    }

    [Fact]
    public void ArrayFactoryShouldPreserveBackingCollections()
    {
        var items = new List<FluidValue>();
        var value = ArrayValue.Create(items);
        Assert.Same(items, value.Values);
        Assert.NotSame(ArrayValue.Empty, value);

        items.Add(NumberValue.Create(1));
        Assert.Single(value.Values);
        Assert.Equal(1, value.Values[0].ToNumberValue());
    }

    [Fact]
    public void ExplicitObjectWrappingShouldNotPerformGeneralConversion()
    {
        var value = ObjectValue.Create("a");
        Assert.Equal(FluidValues.Object, value.Type);
        Assert.Equal("a", value.Value);
        Assert.Same(StringValue.Create("a"), FluidValue.Create("a", TemplateOptions.Default));
    }

    [Fact]
    public async Task DictionaryFactoryShouldPreserveIndexableSnapshot()
    {
        var expected = NumberValue.Create(42);
        var items = new Dictionary<string, FluidValue> { ["key"] = expected };
        var value = DictionaryValue.Create(new FluidValueDictionaryFluidIndexable(items));
        items["key"] = NumberValue.Create(1);

        Assert.Same(expected, await value.GetValueAsync("key", new TemplateContext()));
    }

    [Fact]
    public void GeneralConversionShouldApplyConvertersBeforeCachedValues()
    {
        var calls = 0;
        var converted = StringValue.Create("converted");
        var options = new TemplateOptionsBuilder()
            .AddValueConverter(value =>
            {
                calls++;
                return value is string ? converted : null;
            })
            .Build();

        Assert.Same(converted, FluidValue.Create("a", options));
        Assert.Equal(1, calls);
        Assert.Same(converted, FluidValue.Create(converted, options));
        Assert.Same(NilValue.Instance, FluidValue.Create(null, options));
        Assert.Equal(1, calls);
    }

    [Fact]
    public void GeneralConversionShouldCacheEnumCharacters()
    {
        Assert.Same(StringValue.Create("a"), FluidValue.Create(Letter.a, TemplateOptions.Default));
        Assert.Same(StringValue.Create("a"), FluidValue.Create('a', TemplateOptions.Default));
    }

    [Fact]
    public void FormattableConversionShouldUseConfiguredCulture()
    {
        var culture = CultureInfo.GetCultureInfo("fr-FR");
        var options = new TemplateOptionsBuilder().WithCultureInfo(culture).Build();

        Assert.Equal(12.5m.ToString(culture), FluidValue.Create(new FormattableNumber(), options).ToStringValue());
    }

    [Fact]
    public void TimeSpanConversionShouldUseConfiguredTimeZone()
    {
        var zone = TimeZoneInfo.CreateCustomTimeZone("FactoryTests", TimeSpan.FromHours(2), "FactoryTests", "FactoryTests");
        var options = new TemplateOptionsBuilder().WithTimeZone(zone).Build();
        var span = TimeSpan.FromHours(1);
        var expected = DateTimeOffset.FromUnixTimeMilliseconds((long)span.TotalMilliseconds).ToOffset(zone.BaseUtcOffset);

        Assert.Equal(expected, FluidValue.Create(span, options).ToObjectValue());
    }

    [Fact]
    public async Task EnumerableConversionShouldMaterializeWithConfiguredConverters()
    {
        var calls = 0;
        var options = new TemplateOptionsBuilder()
            .AddValueConverter(value =>
            {
                calls++;
                return value is int number ? NumberValue.Create(number * 2) : null;
            })
            .Build();
        var value = FluidValue.Create(Enumerable.Range(1, 2), options);
        var context = new TemplateContext(options);

        Assert.Equal(FluidValues.Array, value.Type);
        Assert.Equal(3, calls);
        Assert.Equal(2, (await value.GetIndexAsync(NumberValue.Create(0), context)).ToNumberValue());
        Assert.Equal(4, (await value.GetValueAsync("last", context)).ToNumberValue());
        Assert.Equal(3, calls);
    }

    [Fact]
    public void DateTimeFactoriesShouldPreserveOffsetsAndBoundaries()
    {
        var date = new DateTime(2020, 1, 2, 3, 4, 5, DateTimeKind.Utc);
        var offset = new DateTimeOffset(date).ToOffset(TimeSpan.FromHours(2));

        Assert.Equal(new DateTimeOffset(date), DateTimeValue.Create(date).ToObjectValue());
        Assert.Equal(offset, DateTimeValue.Create(offset).ToObjectValue());
        Assert.Equal(DateTime.MinValue.Ticks, ((DateTimeOffset)DateTimeValue.Create(DateTime.MinValue).ToObjectValue()).Ticks);
        Assert.Equal(DateTime.MaxValue.Ticks, ((DateTimeOffset)DateTimeValue.Create(DateTime.MaxValue).ToObjectValue()).Ticks);
        Assert.Equal(DateTimeValue.Create(date), FluidValue.Create(date, TemplateOptions.Default));
        Assert.Equal(DateTimeValue.Create(offset), FluidValue.Create(offset, TemplateOptions.Default));
    }

    [Fact]
    public void FactoryValuesShouldEvaluateLazilyOnce()
    {
        var calls = 0;
        var value = FactoryValue.Create(() =>
        {
            calls++;
            return NumberValue.Create(42);
        });

        Assert.Equal(0, calls);
        Assert.Equal(42, value.ToNumberValue());
        Assert.Equal(42, value.ToNumberValue());
        Assert.Equal(1, calls);
    }

    [Fact]
    public async Task FunctionFactoriesShouldSupportBothDelegateShapes()
    {
        var expected = StringValue.Create("a");
        var context = new TemplateContext();
        var synchronous = FunctionValue.Create((_, c) =>
        {
            Assert.Same(context, c);
            return expected;
        });
        var asynchronous = FunctionValue.Create(async (_, c) =>
        {
            await Task.Yield();
            Assert.Same(context, c);
            return expected;
        });

        Assert.Same(expected, await synchronous.InvokeAsync(null, context));
        Assert.Same(expected, await asynchronous.InvokeAsync(null, context));
    }

    [Fact]
    public void LoopFactoriesShouldCreateIndependentState()
    {
        var first = ForLoopValue.Create();
        var second = ForLoopValue.Create();
        first.Index = 42;
        Assert.NotSame(first, second);
        Assert.Equal(0, second.Index);

        var row = TableRowLoopValue.Create(3, 2);
        Assert.NotSame(row, TableRowLoopValue.Create(3, 2));
        Assert.Equal(3, row.Length);
        Assert.Equal(1, row.Index);
        Assert.Equal(1, row.Col);
        Assert.Equal(1, row.Row);
    }

    [Fact]
    public async Task ComparisonFactoryShouldPreserveTruthinessAndRenderedOperand()
    {
        var value = BinaryExpressionFluidValue.Create(StringValue.Create("&"), false);
        Assert.False(value.ToBooleanValue());
        Assert.Equal("false", value.ToStringValue());
        Assert.True(BinaryExpressionFluidValue.Create(null, true).IsNil());

        var context = new TemplateContext().SetValue("value", value);
        var template = new FluidParser().Parse("{{ value }}{% if value %}true{% else %}false{% endif %}");
        Assert.Equal("&amp;false", await template.RenderAsync(context, HtmlEncoder.Default));
    }

    [Fact]
    public void CustomObjectSubclassesShouldRemainSupported()
    {
        var instance = new CustomObjectValue("custom");
        Assert.Same(instance, FluidValue.Create(instance, TemplateOptions.Default));
        Assert.Equal("custom", instance.Value);
    }

    private enum Letter
    {
        a
    }

    private sealed class FormattableNumber : IFormattable
    {
        public string ToString(string format, IFormatProvider formatProvider) => 12.5m.ToString(formatProvider);
    }

    private sealed class CustomObjectValue : ObjectValueBase
    {
        public CustomObjectValue(object value) : base(value)
        {
        }
    }
}
