using System.Collections.Generic;

namespace Fluid.Values
{
    public class ObjectDictionaryFluidIndexable : IFluidIndexable
    {
        private readonly Dictionary<string, object> _dictionary;

        public ObjectDictionaryFluidIndexable(Dictionary<string, object> dictionary)
        {
            _dictionary = dictionary;
        }

        public ObjectDictionaryFluidIndexable(IDictionary<string, object> dictionary)
        {
            _dictionary = new Dictionary<string, object>(dictionary);
        }


        public int Count => _dictionary.Count;

        public IEnumerable<string> Keys => _dictionary.Keys;

        public bool TryGetValue(string name, out FluidValue value)
        {
            if(_dictionary.TryGetValue(name, out var obj))
            {
                value = FluidValue.Create(obj);
                return true;
            }

            value = NilValue.Instance;
            return false;
        }
    }
}
