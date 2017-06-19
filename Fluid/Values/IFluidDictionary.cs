using System.Collections;
using System.Collections.Generic;

namespace Fluid.Values
{
    public interface IFluidIndexable
    {
        int Count { get; }
        IEnumerable<string> Keys { get; }
        bool TryGetValue(string name, out FluidValue value);
    }

    public class ObjectDictionaryFluidIndexable : IFluidIndexable
    {
        private readonly IDictionary<string, object> _dictionary;

        public ObjectDictionaryFluidIndexable(IDictionary<string, object> dictionary)
        {
            _dictionary = dictionary;
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

    public class FluidValueDictionaryFluidIndexable : IFluidIndexable
    {
        private readonly IDictionary<string, FluidValue> _dictionary;

        public FluidValueDictionaryFluidIndexable(IDictionary<string, FluidValue> dictionary)
        {
            _dictionary = dictionary;
        }

        public int Count => _dictionary.Count;
        public IEnumerable<string> Keys => _dictionary.Keys;

        public bool TryGetValue(string name, out FluidValue value)
        {
            return _dictionary.TryGetValue(name, out value);
        }
    }

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
