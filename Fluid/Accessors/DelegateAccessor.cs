using System;

namespace Fluid.Accessors
{
    public class DelegateAccessor : IMemberAccessor
    {
        private readonly Func<object, object> _getter;

        public DelegateAccessor(Func<object, object> getter)
        {
            _getter = getter;
        }

        public object Get(object obj)
        {
            return _getter(obj);
        }
    }
}
