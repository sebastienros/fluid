using System;
using System.Collections;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Text.Encodings.Web;
using System.Threading.Tasks;

namespace Fluid.Values
{
    public abstract class FluidValue : IEquatable<FluidValue>
    {
        public static Dictionary<Type, Func<object, FluidValue>> TypeMappings = new Dictionary<Type, Func<object, FluidValue>>();
        public abstract void WriteTo(TextWriter writer, TextEncoder encoder, CultureInfo cultureInfo);

        public abstract bool Equals(FluidValue other);

        public abstract bool ToBooleanValue();

        public abstract double ToNumberValue();

        public abstract string ToStringValue();

        public abstract object ToObjectValue();

        public virtual ValueTask<FluidValue> GetValueAsync(string name, TemplateContext context)
        {
            return new ValueTask<FluidValue>(GetValue(name, context));
        }

        protected virtual FluidValue GetValue(string name, TemplateContext context)
        {
            return NilValue.Instance;
        }

        public virtual ValueTask<FluidValue> GetIndexAsync(FluidValue index, TemplateContext context)
        {
            return new ValueTask<FluidValue>(GetIndex(index, context));
        }

        protected virtual FluidValue GetIndex(FluidValue index, TemplateContext context)
        {
            return NilValue.Instance;
        }

        public abstract FluidValues Type { get; }

        public virtual bool IsNil()
        {
            return false;
        }

        public static FluidValue Create(object value)
        {
            if (value == null)
            {
                return NilValue.Instance;
            }

            if (value is FluidValue fluidValue)
            {
                return fluidValue;
            }

            var typeOfValue = value.GetType();

            // First check for a specific type conversion before falling back 
            // to an automatic one
            if (TypeMappings.TryGetValue(typeOfValue, out var mapping))
            {
                return mapping(value);
            }

            switch (System.Type.GetTypeCode(typeOfValue))
            {
                case TypeCode.Boolean:
                    return BooleanValue.Create(Convert.ToBoolean(value));
                case TypeCode.Decimal:
                case TypeCode.Double:
                case TypeCode.Single:
                    return NumberValue.Create(Convert.ToDouble(value));
                case TypeCode.SByte:
                case TypeCode.Byte:
                case TypeCode.Int16:
                case TypeCode.Int32:
                case TypeCode.Int64:
                case TypeCode.UInt16:
                case TypeCode.UInt32:
                case TypeCode.UInt64:
                    return NumberValue.Create(Convert.ToDouble(value), true);
                case TypeCode.Empty:
                    return NilValue.Instance;
                case TypeCode.Object:

                    if (value == null)
                    {
                        return NilValue.Instance;
                    }

                    switch (value)
                    {
                        case FluidValue fluid:
                            return fluid;

                        case DateTimeOffset dateTimeOffset:
                            return new DateTimeValue((DateTimeOffset)value);

                        case IDictionary<string, object> dictionary:
                            return new DictionaryValue(new ObjectDictionaryFluidIndexable(dictionary));

                        case IDictionary<string, FluidValue> fluidDictionary:
                            return new DictionaryValue(new FluidValueDictionaryFluidIndexable(fluidDictionary));

                        case IDictionary otherDictionary:
                            return new DictionaryValue(new DictionaryDictionaryFluidIndexable(otherDictionary));

                        case IList<FluidValue> list:
                            return new ArrayValue(list);

                        case IEnumerable<FluidValue> enumerable:
                            return new ArrayValue(enumerable);

                        case IEnumerable enumerable:
                            var fluidValues = new List<FluidValue>();
                            foreach(var item in enumerable)
                            {
                                fluidValues.Add(Create(item));
                            }

                            return new ArrayValue(fluidValues);
                    }

                    return new ObjectValue(value);
                case TypeCode.DateTime:
                    return new DateTimeValue((DateTime)value);
                case TypeCode.Char:
                case TypeCode.String:
                    return new StringValue(Convert.ToString(value, CultureInfo.InvariantCulture));
                default:
                    throw new InvalidOperationException();
            }
        }

        public virtual bool Contains(FluidValue value)
        {
            // Used by the 'contains' keyword
            return false;
        }

        public virtual IEnumerable<FluidValue> Enumerate()
        {
            return Array.Empty<FluidValue>();
        }

        public FluidValue Or(FluidValue other)
        {
            if (IsNil())
            {
                return other;
            }

            return this;
        }
    }
}
