using System.Globalization;
using System.Text.Encodings.Web;
using System.Text.Json.Serialization;

namespace Fluid.Values;

/// <summary>
/// A wrapper FluidValue that holds a TemplateContext for JSON serialization.
/// This allows the JSON converter to access the TemplateContext without requiring it in the constructor.
/// </summary>
internal sealed class SerializableFluidValue : FluidValue
{
    private readonly FluidValue _inner;

    [JsonIgnore]
    public TemplateContext Context { get; }

    public static SerializableFluidValue Create(FluidValue inner, TemplateContext context)
        => new SerializableFluidValue(inner, context);

    private SerializableFluidValue(FluidValue inner, TemplateContext context)
    {
        _inner = inner ?? throw new ArgumentNullException(nameof(inner));
        Context = context ?? throw new ArgumentNullException(nameof(context));
    }

    public FluidValue InnerValue => _inner;

    public override FluidValues Type => _inner.Type;

    public override bool Equals(FluidValue other) => _inner.Equals(other);

    public override bool ToBooleanValue(TemplateContext context) => _inner.ToBooleanValue(context);

    public override decimal ToNumberValue(TemplateContext context) => _inner.ToNumberValue(context);

    public override string ToStringValue(TemplateContext context) => _inner.ToStringValue(context);

    public override object ToObjectValue(TemplateContext context) => _inner.ToObjectValue(context);

    public override ValueTask<FluidValue> GetValueAsync(string name, TemplateContext context)
        => _inner.GetValueAsync(name, context);

    public override ValueTask<FluidValue> GetIndexAsync(FluidValue index, TemplateContext context)
        => _inner.GetIndexAsync(index, context);

    public override ValueTask<FluidValue> InvokeAsync(FunctionArguments arguments, TemplateContext context)
        => _inner.InvokeAsync(arguments, context);

    public override bool IsNil() => _inner.IsNil();

    public override ValueTask WriteToAsync(IFluidOutput output, TextEncoder encoder, TemplateContext context)
        => _inner.WriteToAsync(output, encoder, context);
}
