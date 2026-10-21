namespace Fluid.Values;

/// <summary>
/// Gives indexed access to the items of an array <see cref="FluidValue"/>.
/// </summary>
/// <remarks>
/// <c>for</c>, <c>tablerow</c>, <c>include ... for</c>, <c>render ... for</c> and the <c>first</c>, <c>last</c>
/// and <c>slice</c> filters read the items of a value implementing this interface by index, instead of
/// enumerating and materializing it. With <c>limit</c>, <c>offset</c> or <c>reversed</c> only the items
/// in the requested range are read, so a value can convert its items on demand.
/// Implement it on a <see cref="FluidValue"/> whose <see cref="FluidValue.Type"/> is <see cref="FluidValues.Array"/>;
/// other values are enumerated.
/// <see cref="ArrayValue"/> is equal to any <see cref="IFluidArray"/> with equal items. Override
/// <see cref="FluidValue.Equals(FluidValue)"/> and <see cref="object.GetHashCode"/> the same way, comparing and hashing
/// the items, so that comparisons give the same result in both directions.
/// </remarks>
public interface IFluidArray
{
    /// <summary>
    /// Gets the number of items.
    /// </summary>
    int Count { get; }

    /// <summary>
    /// Gets the item at <paramref name="index"/>, between <c>0</c> and <see cref="Count"/> excluded.
    /// </summary>
    /// <remarks>Return <see cref="NilValue.Instance"/> for a missing item; <c>null</c> is read as <c>nil</c>.</remarks>
    FluidValue this[int index] { get; }
}
