using Fluid.Values;

namespace Fluid.Accessors;

internal interface IInvoker
{
    object Invoke(object target);
}

internal sealed class Invoker<T, TResult> : IInvoker
{
    private readonly Func<T, TResult> _d;
    private readonly Func<TResult, FluidValue> _converter;

    public Invoker(Delegate d, Func<TResult, FluidValue> converter)
    {
        _d = (Func<T, TResult>)d;
        _converter = converter;
    }

    public object Invoke(object target)
    {
        var result = _d((T)target);
        return _converter != null ? _converter(result) : result;
    }
}
