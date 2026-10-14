using Fluid.Values;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text.Encodings.Web;
using System.Threading.Tasks;
using Xunit;

namespace Fluid.Tests;

public class ArrayOutputEncodingTests
{
    private const string Markup = "<b>x</b>";
    private const string Encoded = "&lt;b&gt;x&lt;/b&gt;";

#if COMPILED
    private static readonly FluidParser _parser = new FluidParser().Compile();
#else
    private static readonly FluidParser _parser = new FluidParser();
#endif

    private static async Task<string> RenderAsync(string source, object items, TextEncoder encoder = null)
    {
        var context = new TemplateContext();
        context.SetValue("items", items);

        return await _parser.Parse(source).RenderAsync(context, encoder ?? HtmlEncoder.Default);
    }

    [Fact]
    public async Task ArrayItemsAreEncoded()
    {
        Assert.Equal(Encoded + "a", await RenderAsync("{{ items }}", new[] { Markup, "a" }));
    }

    [Fact]
    public async Task ArrayIsEncodedLikeItsItemsAre()
    {
        var items = new[] { Markup };

        var whole = await RenderAsync("{{ items }}", items);

        Assert.Equal(whole, await RenderAsync("{{ items[0] }}", items));
        Assert.Equal(whole, await RenderAsync("{{ items | join: '' }}", items));
        Assert.Equal(whole, await RenderAsync("{% for item in items %}{{ item }}{% endfor %}", items));
    }

    [Fact]
    public async Task EnumerableItemsAreEncoded()
    {
        // Not a list, so it is wrapped as an enumerable object rather than an array value.
        var items = new[] { Markup, "a" }.Where(x => x.Length > 0);

        Assert.Equal(Encoded + "a", await RenderAsync("{{ items }}", items));
    }

    [Fact]
    public async Task NestedArrayItemsAreEncoded()
    {
        var items = new object[] { new[] { Markup }, new List<string> { "a", Markup } };

        Assert.Equal(Encoded + "a" + Encoded, await RenderAsync("{{ items }}", items));
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task RawItemsAreNotEncoded(bool enumerable)
    {
        var items = Wrap(
        [
            StringValue.Create(Markup, encode: false),
            StringValue.Create(Markup)
        ], enumerable);

        Assert.Equal(Markup + Encoded, await RenderAsync("{{ items }}", items));
    }

    [Fact]
    public async Task OtherItemsAreWrittenLikeIndividualValues()
    {
        var items = new object[] { 1, 2.50m, true, null, "a" };

        var whole = await RenderAsync("{{ items }}", items);

        Assert.Equal("12.5truea", whole);
        Assert.Equal(whole, await RenderAsync("{% for item in items %}{{ item }}{% endfor %}", items));
    }

    [Fact]
    public async Task NothingIsEncodedWithoutAnEncoder()
    {
        Assert.Equal(Markup + "a", await RenderAsync("{{ items }}", new[] { Markup, "a" }, NullEncoder.Default));
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task NumbersUseTheRenderCulture(bool enumerable)
    {
        var context = new TemplateContext { CultureInfo = CultureInfo.GetCultureInfo("fr-FR") };
        context.SetValue("items", Wrap([NumberValue.Create(2.50m)], enumerable));

        Assert.Equal("2,5", await _parser.Parse("{{ items }}").RenderAsync(context, HtmlEncoder.Default));
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void SynchronousItemsCompleteSynchronously(bool enumerable)
    {
        var item = new ControlledValue();
        item.Completion.SetResult();
        var context = new TemplateContext().SetValue("items", Wrap([item], enumerable));
        var task = _parser.Parse("{{ items }}").RenderAsync(context, HtmlEncoder.Default);

        Assert.True(task.IsCompletedSuccessfully);
        Assert.Equal(Encoded, task.Result);
    }

    [Theory]
    [InlineData(false, false)]
    [InlineData(false, true)]
    [InlineData(true, false)]
    [InlineData(true, true)]
    public async Task CustomItemsAreAwaitedInOrder(bool enumerable, bool nested)
    {
        var item = new ControlledValue();
        FluidValue value = nested ? ArrayValue.Create([item]) : item;
        var items = Wrap([value, StringValue.Create("a")], enumerable);
        var task = RenderAsync("{{ items }}", items);

        Assert.False(task.IsCompleted);
        item.Completion.SetResult();

        Assert.Equal(Encoded + "a", await task);
        Assert.Equal(Encoded + "a", await RenderAsync("{{ items }}", items));
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task CustomItemExceptionsArePropagated(bool enumerable)
    {
        var item = new ControlledValue();
        var task = RenderAsync("{{ items }}", Wrap([item, StringValue.Create("a")], enumerable));
        item.Completion.SetException(new InvalidOperationException("render failed"));

        var exception = await Assert.ThrowsAsync<InvalidOperationException>(() => task);
        Assert.Equal("render failed", exception.Message);
    }

    [Fact]
    public async Task CircularArraysStillThrowAfterAnAsynchronousItem()
    {
        var item = new ControlledValue();
        var values = new List<FluidValue> { item };
        var array = ArrayValue.Create(values);
        values.Add(array);
        var task = RenderAsync("{{ items }}", array);
        item.Completion.SetResult();

        var exception = await Assert.ThrowsAsync<InvalidOperationException>(() => task);
        Assert.Contains("circular", exception.Message, StringComparison.OrdinalIgnoreCase);
    }

    private static object Wrap(FluidValue[] values, bool enumerable) =>
        enumerable ? values.Where(_ => true) : ArrayValue.Create(values);

    private sealed class ControlledValue : ObjectValueBase
    {
        public ControlledValue() : base(new object())
        {
        }

        public TaskCompletionSource Completion { get; } =
            new(TaskCreationOptions.RunContinuationsAsynchronously);

        public override string ToStringValue() => "not the rendered value";

        public override async ValueTask WriteToAsync(IFluidOutput output, TextEncoder encoder, CultureInfo cultureInfo)
        {
            await Completion.Task;
            await StringValue.Create(Markup).WriteToAsync(output, encoder, cultureInfo);
        }
    }
}
