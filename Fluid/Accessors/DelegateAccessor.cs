using System;

namespace Fluid.Accessors
{
    public class DelegateAccessor : DelegateAccessor<object, object>
    {
        public DelegateAccessor(Func<object, string, object> getter) : base((obj, name, _) => getter(obj, name))
        {
        }
    }
}
