using System.Collections;
using System.Diagnostics;
using System.Globalization;
using System.Text.Encodings.Web;
using System.Text.Json.Serialization;

namespace Fluid.Values
{
#pragma warning disable CA1067 // should override Equals because it implements IEquatable<T>
    [JsonConverter(typeof(FluidValueJsonConverter))]
    public abstract class FluidValue : IEquatable<FluidValue>
#pragma warning restore CA1067
    {
        [Obsolete("WriteTo is obsolete, prefer the WriteToAsync method.")]
        public virtual void WriteTo(TextWriter writer, TextEncoder encoder, CultureInfo cultureInfo)
        {
        }

        public virtual ValueTask WriteToAsync(TextWriter writer, TextEncoder encoder, CultureInfo cultureInfo)
        {
#pragma warning disable CS0618 // Type or member is obsolete
            WriteTo(writer, encoder, cultureInfo);
#pragma warning restore CS0618 // Type or member is obsolete
            return default;
        }

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
#pragma warning disable CS0618 // Use obsolete method for backward compatibility
            return GetValue(name, context);
#pragma warning restore CS0618
        }

        public virtual ValueTask<FluidValue> GetIndexAsync(FluidValue index, TemplateContext context)
        {
#pragma warning disable CS0618 // Use obsolete method for backward compatibility
            return GetIndex(index, context);
#pragma warning restore CS0618
        }

        public virtual ValueTask<FluidValue> InvokeAsync(FunctionArguments arguments, TemplateContext context)
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
                    return BooleanValue.Create((bool)value);
                case TypeCode.Byte:
                    return NumberValue.Create((byte)value);
                case TypeCode.UInt16:
                    return NumberValue.Create((ushort)value);
                case TypeCode.UInt32:
                    return NumberValue.Create((uint)value);
                case TypeCode.SByte:
                    return NumberValue.Create((sbyte)value);
                case TypeCode.Int16:
                    return NumberValue.Create((short)value);
                case TypeCode.Int32:
                    return NumberValue.Create((int)value);
                case TypeCode.UInt64:
                    return NumberValue.Create((ulong)value);
                case TypeCode.Int64:
                    return NumberValue.Create((long)value);
                case TypeCode.Double:
                    return NumberValue.Create((decimal)(double)value);
                case TypeCode.Single:
                    return NumberValue.Create((decimal)(float)value);
                case TypeCode.Decimal:
                    return NumberValue.Create((decimal)value);
                case TypeCode.Empty:
                    return NilValue.Instance;
                case TypeCode.Object:

                    switch (value)
                    {
                        case DateTimeOffset dateTimeOffset:
                            return new DateTimeValue(dateTimeOffset);

                        case TimeSpan timeSpan:
                            var baseDateTime = DateTimeOffset.FromUnixTimeMilliseconds((long)timeSpan.TotalMilliseconds).ToOffset(options.TimeZone.BaseUtcOffset);
                            return new DateTimeValue(baseDateTime);

                        case IConvertible convertible:
                            var typeCode = convertible.GetTypeCode();
                            return typeCode switch
                            {
                                TypeCode.Boolean => BooleanValue.Create(convertible.ToBoolean(options.CultureInfo)),
                                TypeCode.Char => new StringValue(convertible.ToString(options.CultureInfo)),
                                TypeCode.SByte => NumberValue.Create(convertible.ToInt32(options.CultureInfo)),
                                TypeCode.Byte => NumberValue.Create(convertible.ToUInt32(options.CultureInfo)),
                                TypeCode.Int16 => NumberValue.Create(convertible.ToInt32(options.CultureInfo)),
                                TypeCode.UInt16 => NumberValue.Create(convertible.ToUInt32(options.CultureInfo)),
                                TypeCode.Int32 => NumberValue.Create(convertible.ToInt32(options.CultureInfo)),
                                TypeCode.UInt32 => NumberValue.Create(convertible.ToUInt32(options.CultureInfo)),
                                TypeCode.Int64 => NumberValue.Create(convertible.ToDecimal(options.CultureInfo)),
                                TypeCode.UInt64 => NumberValue.Create(convertible.ToDecimal(options.CultureInfo)),
                                TypeCode.Single => NumberValue.Create(convertible.ToDecimal(options.CultureInfo)),
                                TypeCode.Double => NumberValue.Create(convertible.ToDecimal(options.CultureInfo)),
                                TypeCode.Decimal => NumberValue.Create(convertible.ToDecimal(options.CultureInfo)),
                                TypeCode.DateTime => new DateTimeValue(convertible.ToDateTime(options.CultureInfo)),
                                TypeCode.String => new StringValue(convertible.ToString(options.CultureInfo)),
                                TypeCode.Object => new StringValue(convertible.ToString(options.CultureInfo)),
                                TypeCode.DBNull => NilValue.Instance,
                                TypeCode.Empty => NilValue.Instance,
                                _ => throw new InvalidOperationException(),
                            };

                        case IFormattable formattable:
                            return new StringValue(formattable.ToString(null, options.CultureInfo));

                        case IDictionary<string, object> dictionary:
                            return new DictionaryValue(new ObjectDictionaryFluidIndexable<object>(dictionary, options));

                        case IDictionary<string, FluidValue> fluidDictionary:
                            return new DictionaryValue(new FluidValueDictionaryFluidIndexable(fluidDictionary));

                        case IDictionary otherDictionary:
                            return new DictionaryValue(new DictionaryDictionaryFluidIndexable(otherDictionary, options));

                        case FluidValue[] array:
                            return array.Length > 0
                                ? new ArrayValue(array)
                                : ArrayValue.Empty;
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
                        case IReadOnlyList<FluidValue> list:
                            if (list.Count == 0)
                            {
                                return ArrayValue.Empty;
                            }

                            return new ArrayValue(list);

                        case IEnumerable<FluidValue> enumerable:
                            return new ArrayValue(enumerable.ToArray());

                        case IList list:
                            if (list.Count == 0)
                            {
                                return ArrayValue.Empty;
                            }

                            var values = new FluidValue[list.Count];
                            for (var i = 0; i < values.Length; i++)
                            {
                                values[i] = Create(list[i], options);
                            }

                            return new ArrayValue(values);

                        case IEnumerable enumerable:
                            List<FluidValue> fluidValues = null;
                            foreach (var item in enumerable)
                            {
                                fluidValues ??= [];
                                fluidValues.Add(Create(item, options));
                            }

                            return fluidValues != null
                                ? new ArrayValue(fluidValues)
                                : ArrayValue.Empty;
                    }

                    return new ObjectValue(value);

                case TypeCode.DateTime:
                    return new DateTimeValue((DateTime)value);

                case TypeCode.Char:
                    return new StringValue(Convert.ToString(value, options.CultureInfo));
                
                case TypeCode.String:
                    return new StringValue((string)value);
                
                default:
                    throw new InvalidOperationException();
            }
        }

        public virtual bool Contains(FluidValue value)
        {
            // Used by the 'contains' keyword
            return false;
        }

        public virtual ValueTask<IEnumerable<FluidValue>> EnumerateAsync(TemplateContext context)
        {
            return new ValueTask<IEnumerable<FluidValue>>(Array.Empty<FluidValue>());
        }

        #region Obsolete members

        [Obsolete("Use EnumerateAsync(TemplateContext) instead.")]
        public virtual IEnumerable<FluidValue> Enumerate(TemplateContext context)
        {
            return EnumerateAsync(context).GetAwaiter().GetResult();
        }

        [Obsolete("Use EnumerateAsync(TemplateContext) instead.")]
        public virtual IEnumerable<FluidValue> Enumerate()
        {
            return [];
        }

        [Obsolete("Use EnumerateAsync(TemplateContext) instead.")]
        internal virtual string[] ToStringArray()
        {
            return [];
        }

        [Obsolete("Use EnumerateAsync(TemplateContext) instead.")]
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
#pragma warning disable CS0618 // Type or member is obsolete
            return Enumerate(context).FirstOrDefault();
#pragma warning restore CS0618 // Type or member is obsolete
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
#pragma warning disable CS0618 // Type or member is obsolete
            return Enumerate(context).LastOrDefault();
#pragma warning restore CS0618 // Type or member is obsolete
        }

        [Obsolete("This method has been deprecated, please use GetValueAsync() instead.")]
        protected virtual FluidValue GetValue(string name, TemplateContext context)
        {
            return NilValue.Instance;
        }

        [Obsolete("This method has been deprecated, please use GetIndexAsync() instead.")]
        protected virtual FluidValue GetIndex(FluidValue index, TemplateContext context)
        {
            return NilValue.Instance;
        }
        #endregion

        public static implicit operator ValueTask<FluidValue>(FluidValue value)
        {
            return new(value);
        }
    }
}
