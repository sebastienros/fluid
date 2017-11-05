using System;

namespace Fluid.Accessors
{
    public class DelegateAccessor : DelegateAccessor<object>
    {
        public DelegateAccessor(Func<object, string, object> getter) : base(getter)
        {
        }
    }
}
