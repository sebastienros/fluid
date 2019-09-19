using System.Collections.Generic;
using System.Threading.Tasks;
using Fluid.Values;

namespace Fluid
{
    public class FilterCollection
    {
        private readonly Dictionary<string, AsyncFilterDelegate> _delegates;

        public FilterCollection(int capacity = 0)
        {
            _delegates = new Dictionary<string, AsyncFilterDelegate>(capacity);
        }

        public int Count => _delegates.Count;

#if NETSTANDARD2_1
        public void EnsureCapacity(int capacity) => _delegates.EnsureCapacity(capacity);
#endif

        public void AddFilter(string name, FilterDelegate d)
        {
            _delegates[name] = (input, arguments, context) => new ValueTask<FluidValue>(d(input, arguments, context));
        }

        public void AddAsyncFilter(string name, AsyncFilterDelegate d)
        {
            _delegates[name] = d;
        }

        public bool TryGetValue(string name, out AsyncFilterDelegate filter)
        {
            return _delegates.TryGetValue(name, out filter);
        }
    }
}
