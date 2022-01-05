using System;
using System.Threading.Tasks;

namespace Fluid.Accessors
{
    internal sealed class AsyncDelegateAccessor : AsyncDelegateAccessor<object, object>
    {
        public AsyncDelegateAccessor(Func<object, string, Task<object>> getter) : base((obj, name, ctx) => getter(obj, name))
        {
        }
    }
}
