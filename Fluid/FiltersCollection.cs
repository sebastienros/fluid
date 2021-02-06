using System.Collections;
using System.Collections.Generic;

namespace Fluid
{
    public class FilterCollection : IEnumerable<KeyValuePair<string, FilterDelegate>>
    {
        private Dictionary<string, FilterDelegate> _filters;

        public FilterCollection(int capacity = 0)
        {
            if (capacity != 0)
            {
                _filters = new Dictionary<string, FilterDelegate>(capacity);
            }
        }

        public int Count => _filters == null ? 0 : _filters.Count;

#if NETSTANDARD2_1
        public void EnsureCapacity(int capacity) => _filters.EnsureCapacity(capacity);
#endif

        public void AddFilter(string name, FilterDelegate d)
        {
            _filters ??= new Dictionary<string, FilterDelegate>();

            _filters[name] = d;
        }

        public bool TryGetValue(string name, out FilterDelegate filter)
        {
            filter = null;

            return _filters != null && _filters.TryGetValue(name, out filter);
        }

        public void Remove(string name)
        {
            if (_filters != null)
            {
                _filters.Remove(name);
            }
        }

        public void Clear()
        {
            if (_filters != null)
            {
                _filters.Clear();
            }
        }

        public IEnumerator<KeyValuePair<string, FilterDelegate>> GetEnumerator()
        {
            return _filters.GetEnumerator();
        }

        IEnumerator IEnumerable.GetEnumerator()
        {
            return _filters.GetEnumerator();
        }
    }
}
