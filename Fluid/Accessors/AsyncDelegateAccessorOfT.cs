using System;
using System.Threading.Tasks;

namespace Fluid.Accessors
{
    public class AsyncDelegateAccessor<T> : IAsyncMemberAccessor
    {
        private readonly Func<T, string, TemplateContext, Task<object>> _getter;

        public AsyncDelegateAccessor(Func<T, string, TemplateContext, Task<object>> getter)
        {
            _getter = getter;
        }

        public object Get(object obj, string name, TemplateContext ctx)
        {
            throw new NotImplementedException();
        }

        public Task<object> GetAsync(T obj, string name, TemplateContext ctx)
        {
            return _getter(obj, name, ctx);
        }

        Task<object> IAsyncMemberAccessor.GetAsync(object obj, string name, TemplateContext ctx)
        {
            return _getter((T)obj, name, ctx);
        }
    }
}
