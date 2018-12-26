using System.Collections.Generic;

namespace Fluid.Values
{
    public class FluidValueDictionaryFluidIndexable : IFluidIndexable
    {
        private readonly Dictionary<string, FluidValue> _dictionary;

        public FluidValueDictionaryFluidIndexable(Dictionary<string, FluidValue> dictionary)
        {
            _dictionary = dictionary;
        }

        public FluidValueDictionaryFluidIndexable(IDictionary<string, FluidValue> dictionary)
        {
            _dictionary = new Dictionary<string, FluidValue>(dictionary);
        }

        public int Count => _dictionary.Count;

        public IEnumerable<string> Keys => _dictionary.Keys;

        public bool TryGetValue(string name, out FluidValue value)
        {
            return _dictionary.TryGetValue(name, out value);
        }
    }
}
