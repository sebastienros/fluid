using System.Threading.Tasks;

namespace Fluid.Utils
{
    internal static class TaskExtensions
    {
        public static bool IsCompletedSuccessfully(this Task t)
        {
#if !NETSTANDARD2_0
            return t.IsCompletedSuccessfully;
#else
            return t.Status == TaskStatus.RanToCompletion && !t.IsFaulted && !t.IsCanceled;
#endif
        }
    }
}