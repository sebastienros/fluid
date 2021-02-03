using System;
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text.Encodings.Web;
using System.Threading.Tasks;

namespace Fluid.Values
{
    public abstract class FluidValue : IEquatable<FluidValue>
    {
        private static Dictionary<Type, Func<object, FluidValue>> _customTypeMappings;
        private static readonly object _synLock = new object();

        /// <summary>
        /// Gets the list of value converters.
        /// </summary>
        public static List<Func<object, object>> ValueConverters { get; } = new List<Func<object, object>>();

        public abstract void WriteTo(TextWriter writer, TextEncoder encoder, CultureInfo cultureInfo);

        [Conditional("DEBUG")]
        protected static void AssertWriteToParameters(TextWriter writer, TextEncoder encoder, CultureInfo cultureInfo)
        {
            if (writer == null)
            {
                ExceptionHelper.ThrowArgumentNullException(nameof(writer));
            }

            if (encoder == null)
            {
                ExceptionHelper.ThrowArgumentNullException(nameof(encoder));
            }

            if (cultureInfo == null)
            {
                ExceptionHelper.ThrowArgumentNullException(nameof(cultureInfo));
            }
        }

        public abstract bool Equals(FluidValue other);

        public abstract bool ToBooleanValue();

        public abstract decimal ToNumberValue();

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

        public bool IsInteger()
        {
            // Maps to https://github.com/Shopify/liquid/blob/1feaa6381300d56e2c71b49ad8fee0d4b625147b/lib/liquid/utils.rb#L38

            if (Type == FluidValues.Number)
            {
                return NumberValue.GetScale(ToNumberValue()) == 0;
            }

            if (IsNil())
            {
                return false;
            }

            var s = ToStringValue();

            if (String.IsNullOrWhiteSpace(s))
            {
                return false;
            }

            return int.TryParse(s, out var _);
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

            if (ValueConverters.Count > 0)
            {
                foreach (var valueConverter in ValueConverters)
                {
                    var result = valueConverter(value);

                    if (result != null)
                    {
                        // If a converter returned a FluidValue instance use it directly
                        if (result is FluidValue resultFluidValue)
                        {
                            return resultFluidValue;
                        }

                        // Otherwise stop custom conversions
                        break;
                    }
                }
            }

            var typeOfValue = value.GetType();

            // Check for a specific type conversion before falling back to an automatic one
            var mapping = GetTypeMapping(typeOfValue);

            if (mapping != null)
            {
                return mapping(value);
            }

            switch (System.Type.GetTypeCode(typeOfValue))
            {
                case TypeCode.Boolean:
                    return BooleanValue.Create(Convert.ToBoolean(value));
                case TypeCode.Byte:
                case TypeCode.UInt16:
                case TypeCode.UInt32:
                    return NumberValue.Create(Convert.ToUInt32(value));
                case TypeCode.SByte:
                case TypeCode.Int16:
                case TypeCode.Int32:
                    return NumberValue.Create(Convert.ToInt32(value));
                case TypeCode.UInt64:
                case TypeCode.Int64:
                case TypeCode.Decimal:
                case TypeCode.Double:
                case TypeCode.Single:
                    return NumberValue.Create(Convert.ToDecimal(value));
                case TypeCode.Empty:
                    return NilValue.Instance;
                case TypeCode.Object:

                    switch (value)
                    {
                        case DateTimeOffset dateTimeOffset:
                            return new DateTimeValue(dateTimeOffset);

                        case IDictionary<string, object> dictionary:
                            return new DictionaryValue(new ObjectDictionaryFluidIndexable(dictionary));

                        case IDictionary<string, FluidValue> fluidDictionary:
                            return new DictionaryValue(new FluidValueDictionaryFluidIndexable(fluidDictionary));

                        case IDictionary otherDictionary:
                            return new DictionaryValue(new DictionaryDictionaryFluidIndexable(otherDictionary));

                        case FluidValue[] array:
                            return new ArrayValue(array);

                        case IList<FluidValue> list:
                            return new ArrayValue(list);

                        case IEnumerable<FluidValue> enumerable:
                            return new ArrayValue(enumerable);

                        case IList list:
                            var values = new List<FluidValue>(list.Count);
                            foreach (var item in list)
                            {
                                values.Add(Create(item));
                            }
                            return new ArrayValue(values);

                        case IEnumerable enumerable:
                            var fluidValues = new List<FluidValue>();
                            foreach (var item in enumerable)
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

        internal virtual string[] ToStringArray()
        {
            return Array.Empty<string>();
        }

        internal virtual List<FluidValue> ToList()
        {
            return Enumerate().ToList();
        }

        internal virtual FluidValue FirstOrDefault()
        {
            return Enumerate().FirstOrDefault();
        }

        internal virtual FluidValue LastOrDefault()
        {
            return Enumerate().LastOrDefault();
        }

        public FluidValue Or(FluidValue other)
        {
            if (IsNil())
            {
                return other;
            }

            return this;
        }

        /// <summary>
        /// Defines a custom type mapping that is used when converting an <see cref="object"/> to a <see cref="FluidValue"/>.
        /// </summary>
        public static void SetTypeMapping(Type type, Func<object, FluidValue> mapping)
        {
            // We queue concurrent calls so we don't lose an entry
            lock (_synLock)
            {
                if (_customTypeMappings == null)
                {
                    _customTypeMappings = new Dictionary<Type, Func<object, FluidValue>>();
                }

                // We clone the existing list so we don't change it if it's used by a reader
                var newMappings = new Dictionary<Type, Func<object, FluidValue>>(_customTypeMappings)
                {
                    [type] = mapping
                };

                // Then we switch the one that is used to read with the new one
                _customTypeMappings = newMappings;
            }
        }

        /// <summary>
        /// Defines a custom type mapping that is used when converting an instance to a <see cref="FluidValue"/>.
        /// </summary>
        public static void SetTypeMapping<T>(Func<T, FluidValue> mapping)
        {
            SetTypeMapping(typeof(T), t => mapping((T)t));
        }

        /// <summary>
        /// Returns a type mapping, or <code>null</code> if it doesn't exist.
        /// </summary>
        private static Func<object, FluidValue> GetTypeMapping(Type type)
        {
            // Get a local reference in case it is being altered.
            var localTypeMappings = _customTypeMappings;

            if (localTypeMappings == null || localTypeMappings.Count == 0)
            {
                return null;
            }

            if (localTypeMappings.TryGetValue(type, out var mapping))
            {
                return mapping;
            }

            return null;
        }

        public static implicit operator ValueTask<FluidValue>(FluidValue value) => new(value);
    }
}
