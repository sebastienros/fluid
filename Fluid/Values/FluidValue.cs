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
        public virtual ValueTask WriteToAsync(IFluidOutput output, TextEncoder encoder, CultureInfo cultureInfo)
        {
            return default;
        }

        private static Dictionary<Type, Type> _genericDictionaryTypeCache = new();

        [Conditional("DEBUG")]
        protected static void AssertWriteToParameters(IFluidOutput output, TextEncoder encoder, CultureInfo cultureInfo)
        {
            ArgumentNullException.ThrowIfNull(output);
            ArgumentNullException.ThrowIfNull(encoder);
            ArgumentNullException.ThrowIfNull(cultureInfo);
        }

        public abstract bool Equals(FluidValue other);

        [Obsolete("Use ToBooleanValue(TemplateContext) instead.")]
        public abstract bool ToBooleanValue();

        [Obsolete("Use ToNumberValue(TemplateContext) instead.")]
        public abstract decimal ToNumberValue();

        [Obsolete("Use ToStringValue(TemplateContext) instead.")]
        public abstract string ToStringValue();

        [Obsolete("Use ToObjectValue(TemplateContext) instead.")]
        public abstract object ToObjectValue();

        public virtual bool ToBooleanValue(TemplateContext context)
        {
#pragma warning disable CS0618 // Type or member is obsolete
            return ToBooleanValue();
#pragma warning restore CS0618 // Type or member is obsolete
        }

        public virtual decimal ToNumberValue(TemplateContext context)
        {
#pragma warning disable CS0618 // Type or member is obsolete
            return ToNumberValue();
#pragma warning restore CS0618 // Type or member is obsolete
        }

        public virtual string ToStringValue(TemplateContext context)
        {
#pragma warning disable CS0618 // Type or member is obsolete
            return ToStringValue();
#pragma warning restore CS0618 // Type or member is obsolete
        }

        public virtual object ToObjectValue(TemplateContext context)
        {
#pragma warning disable CS0618 // Type or member is obsolete
            return ToObjectValue();
#pragma warning restore CS0618 // Type or member is obsolete
        }

        public virtual ValueTask<FluidValue> GetValueAsync(string name, TemplateContext context)
        {
            return NilValue.Instance;
        }

        public virtual ValueTask<FluidValue> GetIndexAsync(FluidValue index, TemplateContext context)
        {
            return NilValue.Instance;
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

            // Check if the value is an enum and convert to string
            if (typeOfValue.IsEnum)
            {
                return new StringValue(value.ToString());
            }

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

        [Obsolete("Use ContainsAsync(FluidValue, TemplateContext) instead.")]
        public virtual bool Contains(FluidValue value)
        {
            // Used by the 'contains' keyword
            return ContainsAsync(value, null).GetAwaiter().GetResult();
        }

        public virtual ValueTask<bool> ContainsAsync(FluidValue value, TemplateContext context)
        {
            // Used by the 'contains' keyword
            return new ValueTask<bool>(false);
        }

        public virtual async IAsyncEnumerable<FluidValue> EnumerateAsync(TemplateContext context)
        {
            await Task.CompletedTask;
            yield break;
        }

        [Obsolete("Use EnumerateAsync instead")]
        public virtual IEnumerable<FluidValue> Enumerate(TemplateContext context)
        {
            return EnumerateAsync(context).ToEnumerable();
        }

        public static implicit operator ValueTask<FluidValue>(FluidValue value)
        {
            return new(value);
        }
    }
}
