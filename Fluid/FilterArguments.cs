using System.Collections.Generic;
using System.Linq;
using System.Runtime.CompilerServices;
using Fluid.Values;

namespace Fluid
{
    /// <summary>
    /// Represents the list of arguments that are passed to a <see cref="FilterDelegate"/>
    /// when invoked.
    /// </summary>
    public class FilterArguments
    {
        public readonly static FilterArguments Empty = new FilterArguments();

        private List<FluidValue> _positional;
        private Dictionary<string, FluidValue> _named;

        public int Count => _positional != null ? _positional.Count : 0;

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
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

        public FilterArguments()
        {
        }

        public FilterArguments(params FluidValue[] values)
        {
            _positional = new List<FluidValue>(values);  
        }

        public FilterArguments(params object[] values)
        {
            if (values == null)
            {
                _positional = new List<FluidValue>();
                return;
            }

            _positional = new List<FluidValue>(values.Length);

            var length = values.Length;
            for (var i = 0; i < length; i++)
            {
                _positional.Add(FluidValue.Create(values[i]));
            }
        }

        public FilterArguments Add(object value)
        {
            _positional.Add(FluidValue.Create(value));

            return this;
        }

        public FilterArguments Add(string name, object value)
        {
            return Add(name, FluidValue.Create(value));
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

        public IEnumerable<string> Names => _named?.Keys ?? Enumerable.Empty<string>();

        public IEnumerable<FluidValue> Values => _positional;
    }
}
