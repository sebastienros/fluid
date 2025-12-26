using Fluid.Values;

namespace Fluid.Accessors;

internal abstract class Invoker
{
    public abstract object Invoke(object target, TemplateOptions options);
}

internal sealed class Invoker<T, TResult> : Invoker
{
    private readonly Func<T, TResult> _d;
    private readonly Func<TResult, TemplateOptions, FluidValue> _converter;

    public Invoker(Delegate d, Func<TResult, TemplateOptions, FluidValue> converter)
    {
        _d = (Func<T, TResult>)d;
        _converter = converter;
    }

    public override object Invoke(object target, TemplateOptions options)
    {
        var result = _d((T)target);
        return _converter != null ? _converter(result, options) : result;
    }
}
