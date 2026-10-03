using System.Collections;

namespace Fluid
{
    public sealed class FilterCollection : IEnumerable<KeyValuePair<string, FilterDelegate>>
    {
        private static readonly Dictionary<string, FilterDelegate> EmptyFilters = new();

        private Dictionary<string, FilterDelegate> _filters;

        public FilterCollection(int capacity = 0)
        {
            if (capacity != 0)
            {
                _filters = new Dictionary<string, FilterDelegate>(capacity);
            }
        }

        private int _version;
        private bool _readOnly;

        /// <summary>
        /// Gets whether the collection can no longer be modified. The collection exposed by
        /// <see cref="TemplateOptions.Filters"/> is read-only.
        /// </summary>
        public bool IsReadOnly => _readOnly;

        /// <summary>
        /// Creates a read-only copy of the collection.
        /// </summary>
        internal FilterCollection ToReadOnly()
        {
            var copy = new FilterCollection(Count);

            if (_filters != null)
            {
                foreach (var filter in _filters)
                {
                    copy.AddFilter(filter.Key, filter.Value);
                }
            }

            copy._readOnly = true;
            return copy;
        }

        internal FilterCollection ToMutableCopy()
        {
            var copy = ToReadOnly();
            copy._readOnly = false;
            return copy;
        }

        private void ThrowIfReadOnly()
        {
            if (_readOnly)
            {
                throw new InvalidOperationException("The filter collection is read-only. Configure filters with TemplateOptionsBuilder before building the options.");
            }
        }

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
            ThrowIfReadOnly();

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
            ThrowIfReadOnly();

            if (_filters != null)
            {
                _filters.Remove(name);
                Interlocked.Increment(ref _version);
            }
        }

        public void Clear()
        {
            ThrowIfReadOnly();

            if (_filters != null)
            {
                _filters.Clear();
                Interlocked.Increment(ref _version);
            }
        }

        public IEnumerator<KeyValuePair<string, FilterDelegate>> GetEnumerator()
        {
            return (_filters ?? EmptyFilters).GetEnumerator();
        }

        IEnumerator IEnumerable.GetEnumerator()
        {
            return GetEnumerator();
        }
    }
}
