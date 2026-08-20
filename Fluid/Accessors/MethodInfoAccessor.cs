using System.Reflection;
using Fluid.Values;

namespace Fluid.Accessors
{
    public sealed class MethodInfoAccessor : MemberAccessor
    {
        private readonly MethodInfo _methodInfo;

        public MethodInfoAccessor(MethodInfo methodInfo)
        {
            _methodInfo = methodInfo;
        }

        public override ValueTask<FluidValue> GetAsync(object obj, string name, TemplateContext context)
        {
            return CreateValueTask(_methodInfo.Invoke(obj, null), context);
        }
    }

}
