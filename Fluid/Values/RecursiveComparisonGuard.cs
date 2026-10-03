using System.Runtime.CompilerServices;

namespace Fluid.Values
{
    internal static class RecursiveComparisonGuard
    {
        [ThreadStatic]
        private static HashSet<Pair> _pairs;

        public static Scope Enter(FluidValue left, FluidValue right)
        {
            var pairs = _pairs ??= new HashSet<Pair>(PairComparer.Instance);
            var pair = new Pair(left, right);

            if (!pairs.Add(pair))
            {
                ExceptionHelper.ThrowRecursiveValueException();
            }

            return new Scope(pair);
        }

        internal readonly record struct Pair(FluidValue Left, FluidValue Right);

        internal readonly struct Scope : IDisposable
        {
            private readonly Pair _pair;

            internal Scope(Pair pair)
            {
                _pair = pair;
            }

            public void Dispose()
            {
                _pairs.Remove(_pair);
                if (_pairs.Count == 0)
                {
                    _pairs = null;
                }
            }
        }

        private sealed class PairComparer : IEqualityComparer<Pair>
        {
            public static readonly PairComparer Instance = new();

            public bool Equals(Pair x, Pair y) =>
                ReferenceEquals(x.Left, y.Left) && ReferenceEquals(x.Right, y.Right);

            public int GetHashCode(Pair pair) =>
                HashCode.Combine(
                    RuntimeHelpers.GetHashCode(pair.Left),
                    RuntimeHelpers.GetHashCode(pair.Right));
        }
    }
}
