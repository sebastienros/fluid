using System.Runtime.CompilerServices;

namespace Fluid.Values
{
    internal static class RecursiveValueGuard
    {
        private const int MaximumDepth = 100;

        [ThreadStatic]
        private static HashSet<FluidValue> _values;

        public static Scope Enter(FluidValue value)
        {
            var values = _values ??= new HashSet<FluidValue>(ReferenceComparer.Instance);

            if (values.Count >= MaximumDepth || !values.Add(value))
            {
                ExceptionHelper.ThrowRecursiveValueException();
            }

            return new Scope(value);
        }

        internal readonly struct Scope : IDisposable
        {
            private readonly FluidValue _value;

            public Scope(FluidValue value)
            {
                _value = value;
            }

            public void Dispose()
            {
                _values.Remove(_value);
                if (_values.Count == 0)
                {
                    _values = null;
                }
            }
        }

        private sealed class ReferenceComparer : IEqualityComparer<FluidValue>
        {
            public static readonly ReferenceComparer Instance = new();

            public bool Equals(FluidValue x, FluidValue y) => ReferenceEquals(x, y);

            public int GetHashCode(FluidValue obj) => RuntimeHelpers.GetHashCode(obj);
        }
    }
}
