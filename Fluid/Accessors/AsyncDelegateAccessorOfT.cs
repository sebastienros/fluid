using System;
using System.Threading.Tasks;

namespace Fluid.Accessors
{
    public class AsyncDelegateAccessor<T, TResult> : IAsyncMemberAccessor
    {
        private readonly Func<T, string, TemplateContext, Task<TResult>> _getter;

        public AsyncDelegateAccessor(Func<T, string, TemplateContext, Task<TResult>> getter)
        {
            _getter = getter;
        }

        public object Get(object obj, string name, TemplateContext ctx)
        {
            throw new NotImplementedException();
        }

        public Task<TResult> GetAsync(T obj, string name, TemplateContext ctx)
        {
            return _getter(obj, name, ctx);
        }

        async Task<object> IAsyncMemberAccessor.GetAsync(object obj, string name, TemplateContext ctx)
        {
            return await _getter((T)obj, name, ctx);
        }
    }
}
