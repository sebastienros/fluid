using Microsoft.Extensions.Primitives;

namespace Microsoft.Extensions.Primitives
{
    public static class StringSegmentExtensions
    {
        public static char Index(this StringSegment segment, int index)
        {
            if (index < 0)
            {
                return segment.Buffer[segment.Offset + segment.Length + index];
            }

            return segment.Buffer[segment.Offset + index];
        }
    }
}
