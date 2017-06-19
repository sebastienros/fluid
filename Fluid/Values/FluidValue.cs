using System;
using System.Collections;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Text.Encodings.Web;

namespace Fluid.Values
{
    public abstract class FluidValue : IEquatable<FluidValue>
    {
        public static Dictionary<Type, Func<object, FluidValue>> TypeMappings = new Dictionary<System.Type, Func<object, FluidValue>>();
        public abstract void WriteTo(TextWriter writer, TextEncoder encoder);

        public abstract bool Equals(FluidValue other);

        public abstract bool ToBooleanValue();
        public abstract double ToNumberValue();
        public abstract string ToStringValue();
        public abstract object ToObjectValue();

        public virtual FluidValue GetValue(string name, TemplateContext context)
        {
            return NilValue.Instance;
        }

        public virtual FluidValue GetIndex(FluidValue index, TemplateContext context)
        {
            return NilValue.Instance;
        }

        public abstract FluidValues Type { get; }
        public virtual bool IsUndefined()
        {
            return false;
        }

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
                    return new BooleanValue(Convert.ToBoolean(value));
                case TypeCode.Byte:
                case TypeCode.Decimal:
                case TypeCode.Double:
                case TypeCode.Int16:
                case TypeCode.Int32:
                case TypeCode.Int64:
                case TypeCode.SByte:
                case TypeCode.Single:
                case TypeCode.UInt16:
                case TypeCode.UInt32:
                case TypeCode.UInt64:
                    return new NumberValue(Convert.ToDouble(value));
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

                        case IDictionary dictionary:
                            return new DictionaryValue(dictionary);

                        case IDictionary<string, string> dictionary:
                            return new DictionaryValue(dictionary);

                        case IDictionary<string, object> dictionary:
                            return new DictionaryValue(dictionary);

                        case IList list:
                            return new ArrayValue(list);

                        case IEnumerable enumerable:
                            return new ArrayValue(enumerable);
                    }

                    return new ObjectValue(value);
                case TypeCode.DateTime:
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
            if (IsNil() || IsUndefined())
            {
                return other;
            }

            return this;
        }
    }
}
