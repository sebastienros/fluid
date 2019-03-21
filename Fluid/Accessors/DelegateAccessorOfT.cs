using System;

namespace Fluid.Accessors
{
    public class DelegateAccessor<T, TResult> : IMemberAccessor
    {
        private readonly Func<T, string, TemplateContext, TResult> _getter;

        public DelegateAccessor(Func<T, string, TemplateContext, TResult> getter)
        {
            _getter = getter;
        }

        object IMemberAccessor.Get(object obj, string name, TemplateContext ctx)
        {
            return _getter((T)obj, name, ctx);
        }
    }
}
