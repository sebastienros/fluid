using System.Collections;
using System.Collections.Generic;

namespace Fluid.Values
{
    public class DictionaryDictionaryFluidIndexable : IFluidIndexable
    {
        private readonly IDictionary _dictionary;

        public DictionaryDictionaryFluidIndexable(IDictionary dictionary)
        {
            _dictionary = dictionary;
        }

        public int Count => _dictionary.Count;

        public IEnumerable<string> Keys
        {
            get
            {
                foreach (var key in _dictionary.Keys)
                {
                    yield return key.ToString();
                }
            }
        }

        public bool TryGetValue(string name, out FluidValue value)
        {
            if (_dictionary.Contains(name))
            {
                var obj = _dictionary[name];
                value = FluidValue.Create(obj);
                return true;
            }

            value = NilValue.Instance;
            return false;
        }
    }
}
