using System.Runtime.CompilerServices;

namespace Fluid.Utils
{
    internal static class TaskExtensions
    {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static bool IsCompletedSuccessfully(this Task t)
        {
#if NET6_0_OR_GREATER
            return t.IsCompletedSuccessfully;
#else
            return t.Status == TaskStatus.RanToCompletion && !t.IsFaulted && !t.IsCanceled;
#endif
        }
    }
}