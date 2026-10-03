using Fluid.Values;

namespace Fluid.Accessors
{
    public class AsyncDelegateAccessor<T, TResult> : MemberAccessor
    {
        private readonly Func<T, string, TemplateContext, Task<TResult>> _getter;

        public AsyncDelegateAccessor(Func<T, string, TemplateContext, Task<TResult>> getter)
        {
            _getter = getter;
        }

        public Task<TResult> GetAsync(T obj, string name, TemplateContext ctx)
        {
            return _getter(obj, name, ctx);
        }

        public override ValueTask<FluidValue> GetAsync(object obj, string name, TemplateContext context)
        {
            return CreateValueTask(_getter((T)obj, name, context), context);
        }
    }
}
