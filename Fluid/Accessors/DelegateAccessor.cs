namespace Fluid.Accessors
{
    public sealed class DelegateAccessor : DelegateAccessor<object, object>
    {
        public DelegateAccessor(Func<object, string, object> getter) : base((obj, name, ctx) => getter(obj, name))
        {
        }
    }
}
