using System.Reflection;

namespace Fluid.Accessors
{
    public class MethodInfoAccessor : IMemberAccessor
    {
        private readonly MethodInfo _methodInfo;

        public MethodInfoAccessor(MethodInfo methodInfo)
        {
            _methodInfo = methodInfo;
        }

        public object Get(object obj)
        {
            return _methodInfo.Invoke(obj, null);
        }
    }

}
