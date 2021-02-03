using System.Collections.Generic;

namespace Fluid
{
    public class FilterCollection
    {
        private Dictionary<string, AsyncFilterDelegate> _filters;
        private readonly FilterCollection _parent;

        public FilterCollection(int capacity = 0)
        {
            if (capacity != 0)
            {
                _filters = new Dictionary<string, AsyncFilterDelegate>(capacity);
            }
        }

        public FilterCollection(FilterCollection parent, int capacity = 0) : this(capacity)
        {
            _parent = parent;
        }

        public int Count => _filters == null ? 0 : _filters.Count;

#if NETSTANDARD2_1
        public void EnsureCapacity(int capacity) => _filters.EnsureCapacity(capacity);
#endif

        public void AddAsyncFilter(string name, AsyncFilterDelegate d)
        {
            _filters ??= new Dictionary<string, AsyncFilterDelegate>();

            _filters[name] = d;
        }

        public bool TryGetValue(string name, out AsyncFilterDelegate filter)
        {
            filter = null;

            return (_filters != null && _filters.TryGetValue(name, out filter)) || (_parent != null && _parent.TryGetValue(name, out filter));
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
    }
}
