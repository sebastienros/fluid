using System.Globalization;
using System.Text.Encodings.Web;
using System.Text.Json.Serialization;

namespace Fluid.Values
{
    /// <summary>
    /// A wrapper FluidValue that holds a TemplateContext for JSON serialization.
    /// This allows the JSON converter to access the TemplateContext without requiring it in the constructor.
    /// </summary>
    internal sealed class SerializableFluidValue : FluidValue
    {
        private readonly FluidValue _inner;
        
        [JsonIgnore]
        public TemplateContext Context { get; }

        public SerializableFluidValue(FluidValue inner, TemplateContext context)
        {
            _inner = inner ?? throw new ArgumentNullException(nameof(inner));
            Context = context ?? throw new ArgumentNullException(nameof(context));
        }

        public FluidValue InnerValue => _inner;

        public override FluidValues Type => _inner.Type;

        public override bool Equals(FluidValue other) => _inner.Equals(other);

        public override bool ToBooleanValue() => _inner.ToBooleanValue();

        public override decimal ToNumberValue() => _inner.ToNumberValue();

        public override string ToStringValue() => _inner.ToStringValue();

        public override object ToObjectValue() => _inner.ToObjectValue();

        public override ValueTask<FluidValue> GetValueAsync(string name, TemplateContext context) 
            => _inner.GetValueAsync(name, context);

        public override ValueTask<FluidValue> GetIndexAsync(FluidValue index, TemplateContext context) 
            => _inner.GetIndexAsync(index, context);

        public override ValueTask<FluidValue> InvokeAsync(FunctionArguments arguments, TemplateContext context) 
            => _inner.InvokeAsync(arguments, context);

        public override bool IsNil() => _inner.IsNil();

        public override ValueTask WriteToAsync(TextWriter writer, TextEncoder encoder, CultureInfo cultureInfo) 
            => _inner.WriteToAsync(writer, encoder, cultureInfo);
    }
}
