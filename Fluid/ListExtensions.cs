using System;
using System.Collections.Generic;
using System.Runtime.CompilerServices;

namespace Fluid;

internal static class ListExtensions
{
    // we can (ab)use the fact that binary compatibility requires .NET to have fixed layout for type
    private class ListLayout<T>
    {
        public T[] _items = Array.Empty<T>();
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static Span<T> AsSpan<T>(this List<T> list, int start = 0)
    {
#if NET5_0_OR_GREATER
        return System.Runtime.InteropServices.CollectionsMarshal.AsSpan(list).Slice(start);
#else
        return System.Runtime.CompilerServices.Unsafe.As<List<T>, ListLayout<T>>(ref list)._items.AsSpan(start, list.Count - start);
#endif
    }
}