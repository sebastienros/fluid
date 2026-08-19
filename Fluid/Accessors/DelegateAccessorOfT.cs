using Fluid.Values;

namespace Fluid.Accessors
{
    public class DelegateAccessor<T, TResult> : MemberAccessor
    {
        private readonly Func<T, string, TemplateContext, TResult> _getter;

        public DelegateAccessor(Func<T, string, TemplateContext, TResult> getter)
        {
            _getter = getter;
        }

        public override ValueTask<FluidValue> GetAsync(object obj, string name, TemplateContext context)
        {
            return CreateValueTask(_getter((T)obj, name, context), context);
        }
    }
}
