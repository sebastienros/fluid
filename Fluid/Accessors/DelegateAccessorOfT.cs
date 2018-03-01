using System;

namespace Fluid.Accessors
{
    public class DelegateAccessor<T> : IMemberAccessor
    {
        private readonly Func<T, string, TemplateContext, object> _getter;

        public DelegateAccessor(Func<T, string, TemplateContext, object> getter)
        {
            _getter = getter;
        }

        public object Get(T obj, string name, TemplateContext ctx)
        {
            return _getter(obj, name, ctx);
        }

        object IMemberAccessor.Get(object obj, string name, TemplateContext ctx)
        {
            return _getter((T)obj, name, ctx);
        }
    }
}
