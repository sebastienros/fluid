using System.Collections.Generic;
using Fluid.Values;

namespace Fluid
{
    public class FilterArguments
    {
        private List<FluidValue> _positional;
        private Dictionary<string, FluidValue> _named;

        public int Count => _positional != null ? _positional.Count : 0;

        public FluidValue At(int index)
        {
            if (_positional == null || index >= _positional.Count)
            {
                return NilValue.Instance;
            }

            return _positional[index];
        }

        public bool HasNamed(string name)
        {
            return _named != null && _named.ContainsKey(name);
        }

        public FluidValue this[string name]
        {
            get
            {
                if (_named != null && _named.TryGetValue(name, out var value))
                {
                    return value;
                }

                return NilValue.Instance;
            }
        }

        public FilterArguments Add(FluidValue value)
        {
            return Add(null, value);
        }

        public FilterArguments Add(string name, FluidValue value)
        {
            if (name != null)
            {
                if (_named == null)
                {
                    _named = new Dictionary<string, FluidValue>();
                }

                _named.Add(name, value);
            }

            if (_positional == null)
            {
                _positional = new List<FluidValue>();
            }

            _positional.Add(value);

            return this;
        }

        public IEnumerable<string> Names => _named.Keys;
    }
}
