using System.Globalization;
using System.Text.Encodings.Web;

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

        public override ValueTask<IEnumerable<FluidValue>> EnumerateAsync(TemplateContext context)
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

#pragma warning disable CS0618 // Type or member is obsolete
        public override bool ToBooleanValue()
        {
            return _factory.Value.ToBooleanValue();
        }
#pragma warning restore CS0618 // Type or member is obsolete

        public override bool ToBooleanValue(TemplateContext context)
        {
            return _factory.Value.ToBooleanValue(context);
        }

#pragma warning disable CS0618 // Type or member is obsolete
        public override decimal ToNumberValue()
        {
            return _factory.Value.ToNumberValue();
        }
#pragma warning restore CS0618 // Type or member is obsolete

        public override decimal ToNumberValue(TemplateContext context)
        {
            return _factory.Value.ToNumberValue(context);
        }

#pragma warning disable CS0618 // Type or member is obsolete
        public override object ToObjectValue()
        {
            return _factory.Value.ToObjectValue();
        }
#pragma warning restore CS0618 // Type or member is obsolete

        public override object ToObjectValue(TemplateContext context)
        {
            return _factory.Value.ToObjectValue(context);
        }

        public override string ToString()
        {
            return _factory.Value.ToString();
        }

#pragma warning disable CS0618 // Type or member is obsolete
        public override string ToStringValue()
        {
            return _factory.Value.ToStringValue();
        }
#pragma warning restore CS0618 // Type or member is obsolete

        public override string ToStringValue(TemplateContext context)
        {
            return _factory.Value.ToStringValue(context);
        }

        public override ValueTask WriteToAsync(TextWriter writer, TextEncoder encoder, CultureInfo cultureInfo)
        {
            AssertWriteToParameters(writer, encoder, cultureInfo);
            var task = _factory.Value.WriteToAsync(writer, encoder, cultureInfo);

            if (task.IsCompletedSuccessfully)
            {
                return default;
            }

            return Awaited(task);

            static async ValueTask Awaited(ValueTask t)
            {
                await t;
                return;
            }
        }
    }
}
