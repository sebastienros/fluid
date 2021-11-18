using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Text.Encodings.Web;
using System.Threading.Tasks;

namespace Fluid.Values
{
    public sealed class FactoryValue : FluidValue
    {
        private readonly Lazy<FluidValue> _factory;

        public FactoryValue(Func<FluidValue> factory)
        {
            _factory = new Lazy<FluidValue>(() => factory());
        }

        public override FluidValues Type => _factory.Value.Type;

        public override bool Equals(FluidValue other)
        {
            return _factory.Value.Equals(other);
        }

        public override bool Contains(FluidValue value)
        {
            return _factory.Value.Contains(value);
        }

        public override IEnumerable<FluidValue> Enumerate(TemplateContext context)
        {
            return _factory.Value.Enumerate(context);
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

        public override bool ToBooleanValue()
        {
            return _factory.Value.ToBooleanValue();
        }

        public override decimal ToNumberValue()
        {
            return _factory.Value.ToNumberValue();
        }

        public override object ToObjectValue()
        {
            return _factory.Value.ToObjectValue();
        }

        public override string ToString()
        {
            return _factory.Value.ToString();
        }

        public override string ToStringValue()
        {
            return _factory.Value.ToStringValue();
        }

        public override void WriteTo(TextWriter writer, TextEncoder encoder, CultureInfo cultureInfo)
        {
            AssertWriteToParameters(writer, encoder, cultureInfo);
            _factory.Value.WriteTo(writer, encoder, cultureInfo);
        }
    }
}
