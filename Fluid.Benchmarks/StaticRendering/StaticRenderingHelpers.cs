using System;

namespace Fluid.Benchmarks.StaticRendering;

public static class StaticRenderingHelpers
{
    /// <summary>
    /// Truncates a string like Liquid's <c>truncate</c> filter: the result, ellipsis included, is at most
    /// <paramref name="length"/> characters long.
    /// </summary>
    public static string Truncate(string value, int length)
    {
        const string ellipsis = "...";

        if (value.Length <= length)
        {
            return value;
        }

        return string.Concat(value.AsSpan(0, Math.Max(0, length - ellipsis.Length)), ellipsis);
    }
}
