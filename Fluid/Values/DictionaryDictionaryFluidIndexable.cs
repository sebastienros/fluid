using System.Collections;
using System.Collections.Generic;

namespace Fluid.Values
{
    public sealed class DictionaryDictionaryFluidIndexable : IFluidIndexable
    {
        private readonly IDictionary _dictionary;
        private readonly TemplateOptions _options;

        public DictionaryDictionaryFluidIndexable(IDictionary dictionary, TemplateOptions options)
        {
            _dictionary = dictionary;
            _options = options;
        }

        public int Count => _dictionary.Count;

        public IEnumerable<string> Keys
        {
            get
            {
                foreach (var key in _dictionary.Keys)
                {
                    // Only handle string keys since this is what TryGetValue returns
                    if (key is string)
                    {
                        yield return key.ToString();
                    }
                }
            }
        }

        public bool TryGetValue(string name, out FluidValue value)
        {
            if (_dictionary.Contains(name))
            {
                var obj = _dictionary[name];
                value = FluidValue.Create(obj, _options);
                return true;
            }

            value = NilValue.Instance;
            return false;
        }
    }
}
