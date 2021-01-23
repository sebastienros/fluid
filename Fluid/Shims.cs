using System;
using System.Runtime.CompilerServices;

#nullable enable

namespace Fluid
{
    /// <summary>
    /// Filling missing bits between netstandard2.0 and highers libs and frameworks.
    /// </summary>
    internal static class Shims
    {
#if NETSTANDARD2_0
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static string[] Split(this string s, string? separator, StringSplitOptions options = StringSplitOptions.None)
        {
            return s.Split(new[] {separator}, options);
        }
#endif
    }
}