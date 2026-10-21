using System;
using System.Threading.Tasks;
using Xunit;

namespace Fluid.Tests;

public class StructMemberTests
{
#if COMPILED
    private static readonly FluidParser _parser = new FluidParser().Compile();
#else
    private static readonly FluidParser _parser = new FluidParser();
#endif

    public struct Money
    {
        private readonly decimal _amount;

        public Money(decimal amount) => _amount = amount;

        // A field is named like each of these, holding a different value.
        public decimal Amount => _amount / 100m;

        public decimal Doubled => _amount * 2;
    }

    public struct Counter
    {
        private int count;

        public Counter(int value) => count = value;

        public int Count => count + 1;
    }

    public struct Point
    {
        public int X { get; set; }

        public int Y { get; set; }

        public string Label => $"({X}, {Y})";

        public DateTime When { get; set; }
    }

    public readonly record struct Pair(string Left, int Right)
    {
        public string Both => Left + Right;
    }

    private static Task<string> RenderAsync(string source, object value) =>
        _parser.Parse(source).RenderAsync(new TemplateContext().SetValue("v", value)).AsTask();

    [Theory]
    [InlineData("{{ v.Amount }}", "12.34")]
    [InlineData("{{ v.Doubled }}", "2468")]
    public async Task ComputedPropertiesCallTheGetter(string source, string expected)
    {
        Assert.Equal(expected, await RenderAsync(source, new Money(1234m)));
    }

    [Fact]
    public async Task PropertyIsNotReadFromAFieldWithTheSameName()
    {
        Assert.Equal("6", await RenderAsync("{{ v.Count }}", new Counter(5)));
    }

    [Fact]
    public async Task AutoPropertiesAndOtherTypesAreRead()
    {
        var point = new Point { X = 1, Y = 2, When = new DateTime(2024, 1, 2) };

        Assert.Equal("1 2 (1, 2) 2024", await RenderAsync("{{ v.X }} {{ v.Y }} {{ v.Label }} {{ v.When | date: '%Y' }}", point));
    }

    [Fact]
    public async Task RecordStructPropertiesAreRead()
    {
        Assert.Equal("a 3 a3", await RenderAsync("{{ v.Left }} {{ v.Right }} {{ v.Both }}", new Pair("a", 3)));
    }
}
