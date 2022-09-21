using System;
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text.Encodings.Web;
using System.Threading;
using System.Threading.Tasks;

namespace Fluid.Values
{
    public abstract class FluidValue : IEquatable<FluidValue>
    {
        public abstract void WriteTo(TextWriter writer, TextEncoder encoder, CultureInfo cultureInfo);

        private static Dictionary<Type, Type> _genericDictionaryTypeCache = new();

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

        public virtual ValueTask<FluidValue> InvokeAsync(FunctionArguments arguments, TemplateContext context)
        {
            return NilValue.Instance;
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

        public static FluidValue Create(object value, TemplateOptions options)
        {
            if (value == null)
            {
                return NilValue.Instance;
            }

            if (value is FluidValue fluidValue)
            {
                return fluidValue;
            }

            var converters = options.ValueConverters;
            var length = converters.Count;

            for (var i = 0; i < length; i++)
            {
                var valueConverter = converters[i];
                var result = valueConverter(value);

                if (result != null)
                {
                    // If a converter returned a FluidValue instance use it directly
                    if (result is FluidValue resultFluidValue)
                    {
                        return resultFluidValue;
                    }

                    // Otherwise stop custom conversions

                    value = result;
                    break;
                }
            }

            var typeOfValue = value.GetType();

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

                        case TimeSpan timeSpan:
                            return new DateTimeValue(new DateTime(timeSpan.Ticks));

                        case IFormattable formattable:
                            return new StringValue(formattable.ToString(null, options.CultureInfo));

                        case IConvertible convertible:
                            return new StringValue(convertible.ToString(options.CultureInfo));

                        case IDictionary<string, object> dictionary:
                            return new DictionaryValue(new ObjectDictionaryFluidIndexable<object>(dictionary, options));

                        case IDictionary<string, FluidValue> fluidDictionary:
                            return new DictionaryValue(new FluidValueDictionaryFluidIndexable(fluidDictionary));

                        case IDictionary otherDictionary:
                            return new DictionaryValue(new DictionaryDictionaryFluidIndexable(otherDictionary, options));

                        case FluidValue[] array:
                            return new ArrayValue(array);
                    }

                    // Check if it's a more specific IDictionary<string, V>, e.g. JObject
                    var cache = _genericDictionaryTypeCache;

                    if (!cache.TryGetValue(typeOfValue, out var genericType))
                    {
                        foreach (var i in typeOfValue.GetInterfaces())
                        {
                            if (i.IsGenericType && i.GetGenericTypeDefinition() == typeof(IDictionary<,>) && i.GetGenericArguments()[0] == typeof(string))
                            {
                                genericType = typeof(ObjectDictionaryFluidIndexable<>).MakeGenericType(i.GetGenericArguments()[1]);
                                break;
                            }
                        }

                        // Swap the previous cache with a new copy if no other thread has updated the reference.
                        // This ensures the dictionary can only grow and not replace another one of the same size.
                        // Store a null value for non-matching types so we don't try again
                        Interlocked.CompareExchange(ref _genericDictionaryTypeCache, new Dictionary<Type, Type>(cache)
                        {
                            [typeOfValue] = genericType
                        }, cache);
                    }

                    if (genericType != null)
                    {
                        return new DictionaryValue(Activator.CreateInstance(genericType, value, options) as IFluidIndexable);
                    }

                    switch (value)
                    {
                        case IList<FluidValue> list:
                            return new ArrayValue(list);

                        case IEnumerable<FluidValue> enumerable:
                            return new ArrayValue(enumerable);

                        case IList list:
                            var values = new FluidValue[list.Count];
                            for (var i = 0; i < values.Length; i++)
                            {
                                values[i] = Create(list[i], options);
                            }

                            return new ArrayValue(values);

                        case IEnumerable enumerable:
                            var fluidValues = new List<FluidValue>();
                            foreach (var item in enumerable)
                            {
                                fluidValues.Add(Create(item, options));
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

        public virtual IEnumerable<FluidValue> Enumerate(TemplateContext context)
        {
            return Array.Empty<FluidValue>();
        }

        #region Obsolete members

        [Obsolete("Use Enumerate(TemplateContext) instead.")]
        public virtual IEnumerable<FluidValue> Enumerate()
        {
            return Array.Empty<FluidValue>();
        }

        [Obsolete("Use Enumerate(TemplateContext) instead.")]
        internal virtual string[] ToStringArray()
        {
            return Array.Empty<string>();
        }

        [Obsolete("Use Enumerate(TemplateContext) instead.")]
        internal virtual List<FluidValue> ToList()
        {
            return Enumerate().ToList();
        }

        [Obsolete("Handle the property 'first' in GetValueAsync() instead")]
        internal virtual FluidValue FirstOrDefault()
        {
            return Enumerate().FirstOrDefault();
        }

        /// <summary>
        /// Returns the first element. Used by the <code>first</code> filter.
        /// </summary>
        [Obsolete("Handle the property 'first' in GetValueAsync() instead")]
        internal virtual FluidValue FirstOrDefault(TemplateContext context)
        {
            return Enumerate(context).FirstOrDefault();
        }

        [Obsolete("Handle the property 'last' in GetValueAsync() instead")]
        internal virtual FluidValue LastOrDefault()
        {
            return Enumerate().LastOrDefault();
        }

        /// <summary>
        /// Returns the last element. Used by the <code>last</code> filter.
        /// </summary>
        [Obsolete("Handle the property 'last' in GetValueAsync() instead")]
        internal virtual FluidValue LastOrDefault(TemplateContext context)
        {
            return Enumerate(context).LastOrDefault();
        }
        #endregion

        public static implicit operator ValueTask<FluidValue>(FluidValue value) => new(value);
    }
}
