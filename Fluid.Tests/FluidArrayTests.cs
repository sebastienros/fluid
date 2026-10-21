using System.Collections.Generic;
using System.Threading.Tasks;
using Fluid.Tests.Mocks;
using Fluid.Values;
using Xunit;

namespace Fluid.Tests;

public class FluidArrayTests
{
    private static readonly FluidParser _parser = new FluidParser();

    /// <summary>
    /// A host array that converts its items on demand and counts the conversions.
    /// </summary>
    private sealed class CountingArrayValue : FluidValue, IFluidArray
    {
        private readonly int _count;

        public CountingArrayValue(int count)
        {
            _count = count;
        }

        public int Conversions { get; private set; }

        public int Enumerations { get; private set; }

        public int Count => _count;

        public FluidValue this[int index]
        {
            get
            {
                Conversions++;
                return NumberValue.Create(index);
            }
        }

        public override FluidValues Type => FluidValues.Array;

        public override bool Equals(FluidValue other) => ReferenceEquals(this, other);

        public override bool ToBooleanValue() => true;

        public override decimal ToNumberValue() => _count;

        public override string ToStringValue() => "";

        public override object ToObjectValue() => this;

        public override IEnumerable<FluidValue> Enumerate(TemplateContext context)
        {
            Enumerations++;
            for (var i = 0; i < _count; i++)
            {
                yield return this[i];
            }
        }

        public override async IAsyncEnumerable<FluidValue> EnumerateAsync(TemplateContext context)
        {
            Enumerations++;
            for (var i = 0; i < _count; i++)
            {
                yield return this[i];
            }

            await Task.CompletedTask;
        }
    }

    /// <summary>
    /// A host array that can only be enumerated.
    /// </summary>
    private sealed class EnumerableOnlyArrayValue : FluidValue
    {
        private readonly int _count;

        public EnumerableOnlyArrayValue(int count)
        {
            _count = count;
        }

        public override FluidValues Type => FluidValues.Array;

        public override bool Equals(FluidValue other) => ReferenceEquals(this, other);

        public override bool ToBooleanValue() => true;

        public override decimal ToNumberValue() => _count;

        public override string ToStringValue() => "";

        public override object ToObjectValue() => this;

        public override async IAsyncEnumerable<FluidValue> EnumerateAsync(TemplateContext context)
        {
            for (var i = 0; i < _count; i++)
            {
                yield return NumberValue.Create(i);
            }

            await Task.CompletedTask;
        }
    }

    private static async Task<(string Output, CountingArrayValue Items)> RenderAsync(string source, int count = 1000, MockFileProvider provider = null)
    {
        var items = new CountingArrayValue(count);
        var builder = new TemplateOptionsBuilder();
        if (provider is not null)
        {
            builder.WithFileProvider(provider);
        }

        var context = new TemplateContext(builder.Build());
        context.SetValue("items", items);

        var template = _parser.Parse(source);
        var output = await template.RenderAsync(context);
        return (output, items);
    }

    [Theory]
    [InlineData("{% for i in items limit: 3 %}{{ i }},{% endfor %}", "0,1,2,", 3)]
    [InlineData("{% for i in items offset: 10 limit: 2 %}{{ i }},{% endfor %}", "10,11,", 2)]
    [InlineData("{% for i in items reversed limit: 2 %}{{ i }},{% endfor %}", "1,0,", 2)]
    [InlineData("{% for i in items offset: 997 %}{{ i }},{% endfor %}", "997,998,999,", 3)]
    [InlineData("{% for i in items limit: 2 %}{{ i }},{% endfor %}{% for i in items offset: continue limit: 2 %}{{ i }},{% endfor %}", "0,1,2,3,", 4)]
    [InlineData("{% for i in items limit: 5 %}{% if i == 1 %}{% break %}{% endif %}{{ i }},{% endfor %}", "0,", 2)]
    public async Task ForReadsOnlyTheRenderedItems(string source, string expected, int conversions)
    {
        var (output, items) = await RenderAsync(source);

        Assert.Equal(expected, output);
        Assert.Equal(conversions, items.Conversions);
        Assert.Equal(0, items.Enumerations);
    }

    [Fact]
    public async Task ForWithoutItemsRendersElse()
    {
        var (output, items) = await RenderAsync("{% for i in items %}{{ i }}{% else %}empty{% endfor %}", count: 0);

        Assert.Equal("empty", output);
        Assert.Equal(0, items.Enumerations);
    }

    [Fact]
    public async Task TableRowReadsOnlyTheRenderedItems()
    {
        var (output, items) = await RenderAsync("{% tablerow i in items offset: 5 limit: 2 %}{{ i }}{% endtablerow %}");

        Assert.Equal("<tr class=\"row1\">\n<td class=\"col1\">5</td><td class=\"col2\">6</td></tr>\n", output);
        Assert.Equal(2, items.Conversions);
        Assert.Equal(0, items.Enumerations);
    }

    [Fact]
    public async Task RenderForReadsItemsByIndex()
    {
        var provider = new MockFileProvider().Add("item.liquid", "{{ item }},");
        var (output, items) = await RenderAsync("{% render 'item' for items %}", count: 3, provider);

        Assert.Equal("0,1,2,", output);
        Assert.Equal(3, items.Conversions);
        Assert.Equal(0, items.Enumerations);
    }

    [Fact]
    public async Task IncludeForReadsItemsByIndex()
    {
        var provider = new MockFileProvider().Add("item.liquid", "{{ item }},");
        var (output, items) = await RenderAsync("{% include 'item' for items %}", count: 3, provider);

        Assert.Equal("0,1,2,", output);
        Assert.Equal(3, items.Conversions);
        Assert.Equal(0, items.Enumerations);
    }

    [Theory]
    [InlineData("{{ items | first }}", "0", 1)]
    [InlineData("{{ items | last }}", "999", 1)]
    [InlineData("{{ items | slice: 10, 3 | join: ',' }}", "10,11,12", 3)]
    [InlineData("{{ items | slice: -2, 5 | join: ',' }}", "998,999", 2)]
    [InlineData("{{ items | slice: 2000 | size }}", "0", 0)]
    public async Task FiltersReadOnlyTheItemsTheyNeed(string source, string expected, int conversions)
    {
        var (output, items) = await RenderAsync(source);

        Assert.Equal(expected, output);
        Assert.Equal(conversions, items.Conversions);
        Assert.Equal(0, items.Enumerations);
    }

    [Theory]
    [InlineData("{% for i in items limit: 2 %}{{ i }},{% endfor %}", "0,1,")]
    [InlineData("{% tablerow i in items limit: 1 %}{{ i }}{% endtablerow %}", "<tr class=\"row1\">\n<td class=\"col1\">0</td></tr>\n")]
    [InlineData("{{ items | first }}|{{ items | last }}", "0|4")]
    [InlineData("{{ items | slice: 1, 2 | join: ',' }}", "1,2")]
    public async Task EnumerableOnlyArraysAreStillSupported(string source, string expected)
    {
        var context = new TemplateContext();
        context.SetValue("items", new EnumerableOnlyArrayValue(5));

        var output = await _parser.Parse(source).RenderAsync(context);

        Assert.Equal(expected, output);
    }

    [Fact]
    public void ArraysCompareWithIndexedArrays()
    {
        var items = new CountingArrayValue(3);
        var array = ArrayValue.Create([NumberValue.Create(0), NumberValue.Create(1), NumberValue.Create(2)]);

        Assert.True(array.Equals(items));
        Assert.False(ArrayValue.Create([NumberValue.Create(0)]).Equals(items));
    }

    [Fact]
    public void FluidArraysImplementIFluidArray()
    {
        var array = ArrayValue.Create([NumberValue.Create(1), NumberValue.Create(2)]);
        var enumerable = FluidValue.Create(new List<int> { 1, 2 }, TemplateOptions.Default);

        var indexedArray = Assert.IsAssignableFrom<IFluidArray>(array);
        var indexedEnumerable = Assert.IsAssignableFrom<IFluidArray>(enumerable);

        Assert.Equal(2, indexedArray.Count);
        Assert.Equal(2, indexedArray[1].ToNumberValue());
        Assert.Equal(2, indexedEnumerable.Count);
        Assert.Equal(2, indexedEnumerable[1].ToNumberValue());
    }
}
