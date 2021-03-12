using System;
using System.Diagnostics;
using System.Text;

namespace Fluid.Utils
{

    /// <summary>
    /// The usage is:
    ///        var inst = StringBuilderPool.GetInstance();
    ///        var sb = inst.builder;
    ///        ... Do Stuff...
    ///        ... sb.ToString() ...
    ///        inst.Free();
    /// </summary>
    internal sealed class StringBuilderPool : IDisposable
    {
        private const int DefaultPoolCapacity = 40 * 1024;
        private readonly int _defaultCapacity;

        // global pool
        private static readonly ObjectPool<StringBuilderPool> s_poolInstance = CreatePool();

        public readonly StringBuilder Builder;
        private readonly ObjectPool<StringBuilderPool> _pool;

        private StringBuilderPool(ObjectPool<StringBuilderPool> pool, int defaultCapacity)
        {
            Debug.Assert(pool != null);
            _defaultCapacity = defaultCapacity;
            Builder = new StringBuilder(defaultCapacity);
            _pool = pool;
        }

        public int Length => Builder.Length;

        /// <summary>
        /// If someone need to create a private pool
        /// </summary>
        internal static ObjectPool<StringBuilderPool> CreatePool(int size = 100, int capacity = DefaultPoolCapacity)
        {
            ObjectPool<StringBuilderPool> pool = null;
            pool = new ObjectPool<StringBuilderPool>(() => new StringBuilderPool(pool, capacity), size);
            return pool;
        }

        /// <summary>
        /// Returns a StringBuilder from the default pool.
        /// </summary>
        public static StringBuilderPool GetInstance()
        {
            var builder = s_poolInstance.Allocate();
            Debug.Assert(builder.Builder.Length == 0);
            return builder;
        }

        public override string ToString()
        {
            return Builder.ToString();
        }

        public void Dispose()
        {
            var builder = Builder;

            // Do not store builders that are too large.

            if (builder.Capacity == _defaultCapacity)
            {
                builder.Clear();
                _pool.Free(this);
            }
        }
    }
}