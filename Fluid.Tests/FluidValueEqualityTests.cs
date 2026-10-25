using System;
using System.Collections.Generic;
using Fluid.Values;
using Xunit;

namespace Fluid.Tests;

public class FluidValueEqualityTests
{
    public static IEnumerable<object[]> WrappedValues()
    {
        // The built-in values compare a value of the same kind they don't know, such as a wrapper, by asking it.
        yield return [NumberValue.Create(1), NumberValue.Create(1), true];
        yield return [NumberValue.Create(1), NumberValue.Create(2), false];
        yield return [StringValue.Create("a"), StringValue.Create("a"), true];
        yield return [StringValue.Create("a"), StringValue.Create("b"), false];
        yield return [BooleanValue.True, BooleanValue.True, true];
        yield return [BooleanValue.True, BooleanValue.False, false];
        yield return [BlankValue.Instance, StringValue.Create("  "), true];
        yield return [BlankValue.Instance, ArrayValue.Empty, true];
        yield return [BlankValue.Instance, ArrayValue.Create(new[] { NumberValue.Create(1) }), false];
        yield return [EmptyValue.Instance, StringValue.Empty, true];
        yield return [EmptyValue.Instance, ArrayValue.Empty, true];
        yield return [EmptyValue.Instance, StringValue.Create(" "), false];
        yield return [NilValue.Instance, NilValue.Instance, true];
        yield return [NilValue.Instance, NumberValue.Create(0), false];
    }

    [Theory]
    [MemberData(nameof(WrappedValues))]
    public void BuiltInValues_Compare_Wrapped_Values(FluidValue value, FluidValue wrapped, bool expected)
    {
        var factory = FactoryValue.Create(() => wrapped);

        Assert.Equal(expected, value.Equals(factory));
        Assert.Equal(expected, factory.Equals(value));
    }

    [Fact]
    public void BlankAndEmpty_Compare_Custom_Arrays_By_Count()
    {
        Assert.True(BlankValue.Instance.Equals(new CustomArray(0)));
        Assert.True(EmptyValue.Instance.Equals(new CustomArray(0)));
        Assert.False(BlankValue.Instance.Equals(new CustomArray(2)));
        Assert.False(EmptyValue.Instance.Equals(new CustomArray(2)));
    }

    private sealed class CustomArray : FluidValue, IFluidArray
    {
        public CustomArray(int count)
        {
            Count = count;
        }

        public int Count { get; }

        public FluidValue this[int index] => NumberValue.Create(index);

        public override FluidValues Type => FluidValues.Array;

        public override bool Equals(FluidValue other) => ReferenceEquals(this, other);

        public override bool ToBooleanValue(TemplateContext context) => true;

        public override decimal ToNumberValue(TemplateContext context) => Count;

        public override string ToStringValue(TemplateContext context) => "";

        public override object ToObjectValue(TemplateContext context) => Array.Empty<object>();

        public override int GetHashCode() => Count;

        public override bool Equals(object obj) => ReferenceEquals(this, obj);
    }
}
