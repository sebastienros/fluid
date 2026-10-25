using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using BenchmarkDotNet.Attributes;
using Fluid.Values;

namespace Fluid.Benchmarks;

/// <summary>
/// A <c>for</c> loop with <c>limit</c>, and the <c>slice</c> and <c>last</c> filters, over a large host array whose
/// items are converted on demand. <see cref="Indexed"/> compares an array implementing <see cref="IFluidArray"/>
/// with one that can only be enumerated, which is how every host array was read before <see cref="IFluidArray"/>.
/// </summary>
[MemoryDiagnoser]
public class CustomArrayBenchmarks
{
    private const int ItemCount = 32_000;

    private readonly int[] _data = new int[ItemCount];
    private readonly IFluidTemplate _forLimit;
    private readonly IFluidTemplate _forOffsetLimit;
    private readonly IFluidTemplate _slice;
    private readonly IFluidTemplate _last;
    private FluidValue _items;

    [Params(false, true)]
    public bool Indexed { get; set; }

    public CustomArrayBenchmarks()
    {
        for (var i = 0; i < ItemCount; i++)
        {
            _data[i] = i;
        }

        var parser = new FluidParser();
        _forLimit = parser.Parse("{% for item in items limit: 5 %}{{ item }},{% endfor %}");
        _forOffsetLimit = parser.Parse("{% for item in items offset: 16000 limit: 5 %}{{ item }},{% endfor %}");
        _slice = parser.Parse("{{ items | slice: 100, 5 | join: ',' }}");
        _last = parser.Parse("{{ items | last }}");
    }

    [GlobalSetup]
    public void Setup()
    {
        _items = Indexed ? new IndexedHostArrayValue(_data) : new HostArrayValue(_data);

        Verify(nameof(ForLimit), ForLimit(), "0,1,2,3,4,");
        Verify(nameof(ForOffsetLimit), ForOffsetLimit(), "16000,16001,16002,16003,16004,");
        Verify(nameof(Slice), Slice(), "100,101,102,103,104");
        Verify(nameof(Last), Last(), "31999");

        static void Verify(string name, string result, string expected)
        {
            if (result != expected)
            {
                throw new InvalidOperationException($"{name} rendering failed: {result}");
            }
        }
    }

    [Benchmark]
    public string ForLimit()
    {
        return _forLimit.Render(new TemplateContext().SetValue("items", _items));
    }

    [Benchmark]
    public string ForOffsetLimit()
    {
        return _forOffsetLimit.Render(new TemplateContext().SetValue("items", _items));
    }

    [Benchmark]
    public string Slice()
    {
        return _slice.Render(new TemplateContext().SetValue("items", _items));
    }

    [Benchmark]
    public string Last()
    {
        return _last.Render(new TemplateContext().SetValue("items", _items));
    }

    /// <summary>
    /// A host array that converts its items when they are read.
    /// </summary>
    private class HostArrayValue : FluidValue
    {
        protected readonly int[] Data;

        public HostArrayValue(int[] data)
        {
            Data = data;
        }

        public override FluidValues Type => FluidValues.Array;

        public override bool Equals(FluidValue other) => ReferenceEquals(this, other);

        public override bool ToBooleanValue(TemplateContext context) => true;

        public override decimal ToNumberValue(TemplateContext context) => Data.Length;

        public override string ToStringValue(TemplateContext context) => "";

        public override object ToObjectValue(TemplateContext context) => Data;

        public override async IAsyncEnumerable<FluidValue> EnumerateAsync(TemplateContext context)
        {
            foreach (var item in Data)
            {
                yield return NumberValue.Create(item);
            }

            await Task.CompletedTask;
        }
    }

    private sealed class IndexedHostArrayValue : HostArrayValue, IFluidArray
    {
        public IndexedHostArrayValue(int[] data) : base(data)
        {
        }

        public int Count => Data.Length;

        public FluidValue this[int index] => NumberValue.Create(Data[index]);
    }
}
