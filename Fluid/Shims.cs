#if !NET6_0_OR_GREATER
using System.Runtime.CompilerServices;

#nullable enable

namespace Fluid
{
    /// <summary>
    /// Filling missing bits between netstandard2.0 and higher libs and frameworks.
    /// </summary>
    internal static class Shims
    {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static string[] Split(this string s, string? separator, StringSplitOptions options = StringSplitOptions.None)
        {
            return s.Split(new[] { separator }, options);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining | (MethodImplOptions)512)]
        public static bool EndsWith(this string s, char c)
        {
            return s.Length > 0 && s[^1] == c;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static bool Contains(this string s, char c)
        {
            return s.IndexOf(c) != -1;
        }
    }
}
#endif
