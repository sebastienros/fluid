using System.Collections.Generic;

namespace Fluid.Values
{
    public sealed class ObjectDictionaryFluidIndexable<T> : IFluidIndexable
    {
        private readonly IDictionary<string, T> _dictionary;
        private readonly TemplateOptions _options;

        public ObjectDictionaryFluidIndexable(IDictionary<string, T> dictionary, TemplateOptions options)
        {
            _dictionary = dictionary;
            _options = options;
        }

        public int Count => _dictionary.Count;

        public IEnumerable<string> Keys => _dictionary.Keys;

        public bool TryGetValue(string name, out FluidValue value)
        {
            if(_dictionary.TryGetValue(name, out var obj))
            {
                value = FluidValue.Create(obj, _options);
                return true;
            }

            value = NilValue.Instance;
            return false;
        }
    }
}
