using System.Collections.Generic;
using System.Threading.Tasks;
using Fluid.Values;

namespace Fluid
{
    public class FilterCollection
    {
        private Dictionary<string, AsyncFilterDelegate> _delegates = new Dictionary<string, AsyncFilterDelegate>();

        public void AddFilter(string name, FilterDelegate d)
        {
            _delegates.Add(name, (input, arguments, context) => Task.FromResult<FluidValue>(d(input, arguments, context)));
        }

        public void AddAsyncFilter(string name, AsyncFilterDelegate d)
        {
            _delegates.Add(name, d);
        }

        public bool TryGetValue(string name, out AsyncFilterDelegate filter)
        {
            return _delegates.TryGetValue(name, out filter);
        }
    }
}
