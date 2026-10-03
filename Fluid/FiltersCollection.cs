using System.Collections;

namespace Fluid
{
    public sealed class FilterCollection : IEnumerable<KeyValuePair<string, FilterDelegate>>
    {
        private Dictionary<string, FilterDelegate> _filters;

        public FilterCollection(int capacity = 0)
        {
            if (capacity != 0)
            {
                _filters = new Dictionary<string, FilterDelegate>(capacity);
            }
        }

        private int _version;

        /// <summary>
        /// Changes whenever the content of the collection changes, so that call sites can cache a
        /// resolved <see cref="FilterDelegate"/> and detect when the cached value became stale.
        /// </summary>
        /// <remarks>
        /// Read as an acquire so the dictionary probe that follows can't be reordered ahead of it, and
        /// incremented atomically so a preempted writer can't roll the counter back onto a value a call
        /// site has already cached. Note this only orders the version itself: mutating the collection
        /// while templates render concurrently is still unsupported, because the backing dictionary is
        /// not thread-safe.
        /// </remarks>
        internal int Version => Volatile.Read(ref _version);

        public int Count => _filters == null ? 0 : _filters.Count;

        public void AddFilter(string name, FilterDelegate d)
        {
            _filters ??= new Dictionary<string, FilterDelegate>();

            _filters[name] = d;
            Interlocked.Increment(ref _version);
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
                Interlocked.Increment(ref _version);
            }
        }

        public void Clear()
        {
            if (_filters != null)
            {
                _filters.Clear();
                Interlocked.Increment(ref _version);
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
