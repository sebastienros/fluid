#nullable enable

namespace Fluid
{
#if !NET6_0_OR_GREATER
    /// <summary>
    /// Filling missing bits between netstandard2.0 and higher libs and frameworks.
    /// </summary>
    internal static class Shims
    {
        [System.Runtime.CompilerServices.MethodImpl(System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
        public static string[] Split(this string s, string? separator, StringSplitOptions options = StringSplitOptions.None)
        {
            return s.Split([separator], options);
        }

        [System.Runtime.CompilerServices.MethodImpl(System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
        public static bool EndsWith(this string s, char c)
        {
            return s.Length > 0 && s[^1] == c;
        }

        [System.Runtime.CompilerServices.MethodImpl(System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
        public static bool Contains(this string s, char c)
        {
            return s.IndexOf(c) != -1;
        }
    }
#endif

#if !NET9_0_OR_GREATER
    internal sealed class Lock;
#endif

#if !NET8_0_OR_GREATER
    internal sealed class HashCode
    {
        private int _hc = 19;

        public void Add(object o)
        {
            unchecked
            {
                _hc = _hc * 31 + o.GetHashCode();
            }
        }
        public int ToHashCode() => _hc;
    }
#endif
}
