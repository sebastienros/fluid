using System.Globalization;
using System.Text.Encodings.Web;

namespace Fluid.Values
{
    public sealed class ArrayValue : FluidValue
    {
        public static readonly ArrayValue Empty = new ArrayValue([]);

        public override FluidValues Type => FluidValues.Array;

        public ArrayValue(IReadOnlyList<FluidValue> values)
        {
            Values = values ?? [];
        }

        public override bool Equals(FluidValue other)
        {
            if (other.IsNil())
            {
                return Values.Count == 0;
            }

            if (other is ArrayValue arrayValue)
            {
                if (Values.Count != arrayValue.Values.Count)
                {
                    return false;
                }

                for (var i = 0; i < Values.Count; i++)
                {
                    var item = Values[i];
                    var otherItem = arrayValue.Values[i];

                    if (!item.Equals(otherItem))
                    {
                        return false;
                    }
                }

                return true;
            }
            else if (other.Type == FluidValues.Empty)
            {
                return Values.Count == 0;
            }

            return false;
        }

        public override ValueTask<FluidValue> GetValueAsync(string name, TemplateContext context)
        {
            switch (name)
            {
                case "size":
                    return NumberValue.Create(Values.Count);

                case "first":
                    if (Values.Count > 0)
                    {
                        return Values[0];
                    }
                    break;

                case "last":
                    if (Values.Count > 0)
                    {
                        return Values[Values.Count - 1];
                    }
                    break;

            }

            return NilValue.Instance;
        }

        protected override FluidValue GetIndex(FluidValue index, TemplateContext context)
        {
            var i = (int)index.ToNumberValue();

            if (i >= 0 && i < Values.Count)
            {
                return FluidValue.Create(Values[i], context.Options);
            }

            return NilValue.Instance;
        }

        public override bool ToBooleanValue()
        {
            return true;
        }

        public override decimal ToNumberValue()
        {
            return Values.Count;
        }

        public IReadOnlyList<FluidValue> Values { get; }

        [Obsolete("WriteTo is obsolete, prefer the WriteToAsync method.")]
        public override void WriteTo(TextWriter writer, TextEncoder encoder, CultureInfo cultureInfo)
        {
            AssertWriteToParameters(writer, encoder, cultureInfo);

            foreach (var v in Values)
            {
                writer.Write(v.ToStringValue());
            }
        }

        public override async ValueTask WriteToAsync(TextWriter writer, TextEncoder encoder, CultureInfo cultureInfo)
        {
            AssertWriteToParameters(writer, encoder, cultureInfo);

            foreach (var v in Values)
            {
                await writer.WriteAsync(v.ToStringValue());
            }
        }

        public override string ToStringValue()
        {
            return String.Join("", Values.Select(x => x.ToStringValue()));
        }

        public override object ToObjectValue()
        {
            return Values.Select(x => x.ToObjectValue()).ToArray();
        }

        public override bool Contains(FluidValue value)
        {
            return Values.Contains(value);
        }

        public override IEnumerable<FluidValue> Enumerate(TemplateContext context)
        {
            return Values;
        }

        public override bool Equals(object obj)
        {
            // The is operator will return false if null
            if (obj is ArrayValue otherValue)
            {
                return Equals(otherValue);
            }

            return false;
        }

        public override int GetHashCode()
        {
            var hc = new HashCode();

            foreach (var v in Values)
            {
                hc.Add(v);
            }

            return hc.ToHashCode();
        }
    }
}
