using System.Collections.Generic;

namespace Fluid.Values
{
    public interface IFluidIndexable
    {
        int Count { get; }
        IEnumerable<string> Keys { get; }
        bool TryGetValue(string name, out FluidValue value);
    }
}
