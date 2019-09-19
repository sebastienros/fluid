using System.Runtime.CompilerServices;

namespace Microsoft.Extensions.Primitives
{
    public static class StringSegmentExtensions
    {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static char Index(this StringSegment segment, int index)
        {
            var indexToUse = segment.Offset + index;
            if (index < 0)
            {
                indexToUse += segment.Length;
            }

            return segment.Buffer[indexToUse];
        }
    }
}
