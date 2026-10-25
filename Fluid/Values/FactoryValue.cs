using System.Globalization;
using System.Text.Encodings.Web;

namespace Fluid.Values;

public sealed class FactoryValue : FluidValue
{
    private readonly Lazy<FluidValue> _factory;

    public static FactoryValue Create(Func<FluidValue> factory) => new FactoryValue(factory);

    private FactoryValue(Func<FluidValue> factory)
    {
        _factory = new Lazy<FluidValue>(() => factory());
    }

    public override FluidValues Type => _factory.Value.Type;

    public override bool Equals(FluidValue other)
    {
        return _factory.Value.Equals(other);
    }

    public override ValueTask<bool> ContainsAsync(FluidValue value, TemplateContext context)
    {
        return _factory.Value.ContainsAsync(value, context);
    }

    public override IAsyncEnumerable<FluidValue> EnumerateAsync(TemplateContext context)
    {
        return _factory.Value.EnumerateAsync(context);
    }

    public override bool Equals(object obj)
    {
        return _factory.Value.Equals(obj);
    }

    public override int GetHashCode()
    {
        return _factory.Value.GetHashCode();
    }

    public override ValueTask<FluidValue> GetIndexAsync(FluidValue index, TemplateContext context)
    {
        return _factory.Value.GetIndexAsync(index, context);
    }

    public override ValueTask<FluidValue> GetValueAsync(string name, TemplateContext context)
    {
        return _factory.Value.GetValueAsync(name, context);
    }

    public override bool IsNil()
    {
        return _factory.Value.IsNil();
    }

    public override bool ToBooleanValue(TemplateContext context)
    {
        return _factory.Value.ToBooleanValue(context);
    }

    public override decimal ToNumberValue(TemplateContext context)
    {
        return _factory.Value.ToNumberValue(context);
    }

    public override object ToObjectValue(TemplateContext context)
    {
        return _factory.Value.ToObjectValue(context);
    }

    public override string ToString()
    {
        return _factory.Value.ToString();
    }

    public override string ToStringValue(TemplateContext context)
    {
        return _factory.Value.ToStringValue(context);
    }

    public override ValueTask WriteToAsync(IFluidOutput output, TextEncoder encoder, TemplateContext context)
    {
        AssertWriteToParameters(output, encoder, context);
        return _factory.Value.WriteToAsync(output, encoder, context);
    }
}
