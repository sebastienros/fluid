using System;

namespace Fluid.Accessors
{
    public class DelegateAccessor : DelegateAccessor<object>
    {
        public DelegateAccessor(Func<object, string, object> getter) : base(getter)
        {
        }
    }

    public class DelegateAccessor<T> : IMemberAccessor
    {
        private readonly Func<T, string, object> _getter;

        public DelegateAccessor(Func<T, string, object> getter)
        {
            _getter = getter;
        }

        public object Get(T obj, string name)
        {
            return _getter(obj, name);
        }

        object IMemberAccessor.Get(object obj, string name)
        {
            return _getter((T)obj, name);
        }
    }
}
