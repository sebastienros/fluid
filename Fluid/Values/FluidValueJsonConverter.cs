using System.Reflection;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Text.Json.Serialization.Metadata;

namespace Fluid.Values
{
    /// <summary>
    /// JSON converter for all FluidValue types.
    /// </summary>
    /// <remarks>
    /// This converter can be overridden by registering a custom JsonConverter for specific FluidValue types
    /// in the JsonSerializerOptions before the json filter is called.
    /// </remarks>
    internal sealed class FluidValueJsonConverter : JsonConverter<FluidValue>
    {
        /// <summary>
        /// Creates a new instance of FluidValueJsonConverter without a context.
        /// The context will be extracted from SerializableFluidValue instances.
        /// </summary>
        public FluidValueJsonConverter()
        {
        }

        public override FluidValue Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
        {
            throw new NotSupportedException("Deserialization of FluidValue is not supported.");
        }

        public override void Write(Utf8JsonWriter writer, FluidValue value, JsonSerializerOptions options)
        {
            TemplateContext context;
            FluidValue actualValue = value;

            if (value is SerializableFluidValue serializableValue)
            {
                context = serializableValue.Context;
                actualValue = serializableValue.InnerValue;
            }
            else
            {
                // No context available
                context = null;
            }

            switch (actualValue.Type)
            {
                case FluidValues.Array:
                    if (context == null)
                    {
                        throw new InvalidOperationException("A TemplateContext is required to serialize FluidValue instances. Please wrap the FluidValue in a SerializableFluidValue.");
                    }
                    writer.WriteStartArray();
                    foreach (var item in actualValue.Enumerate(context))
                    {
                        var wrapped = new SerializableFluidValue(item, context);
                        JsonSerializer.Serialize<FluidValue>(writer, wrapped, options);
                    }
                    writer.WriteEndArray();
                    break;
                case FluidValues.Boolean:
                    writer.WriteBooleanValue(actualValue.ToBooleanValue());
                    break;
                case FluidValues.Nil:
                    writer.WriteNullValue();
                    break;
                case FluidValues.Number:
                    writer.WriteNumberValue(actualValue.ToNumberValue());
                    break;
                case FluidValues.Dictionary:
                    if (actualValue.ToObjectValue() is not IFluidIndexable dict)
                    {
                        writer.WriteNullValue();
                        break;
                    }

                    writer.WriteStartObject();
                    foreach (var key in dict.Keys)
                    {
                        writer.WritePropertyName(key);

                        if (dict.TryGetValue(key, out var fluidValue) && fluidValue is not null)
                        {
                            var wrapped = new SerializableFluidValue(fluidValue, context);
                            JsonSerializer.Serialize<FluidValue>(writer, wrapped, options);
                        }
                        else
                        {
                            writer.WriteNullValue();
                        }
                    }
                    writer.WriteEndObject();
                    break;
                case FluidValues.Object:
                    WriteObject(writer, actualValue, context, options);
                    break;
                case FluidValues.DateTime:
                    writer.WriteStringValue((DateTimeOffset)actualValue.ToObjectValue());
                    break;
                case FluidValues.String:
                    writer.WriteStringValue(actualValue.ToStringValue());
                    break;
                case FluidValues.Blank:
                    writer.WriteStringValue(string.Empty);
                    break;
                case FluidValues.Empty:
                    writer.WriteStringValue(string.Empty);
                    break;
                default:
                    writer.WriteNullValue();
                    break;
            }
        }

        private static void WriteObject(Utf8JsonWriter writer, FluidValue value, TemplateContext context, JsonSerializerOptions options)
        {
            var obj = value.ToObjectValue();

            if (obj == null)
            {
                writer.WriteNullValue();
                return;
            }

            var type = obj.GetType();
            var typeInfo = options.GetTypeInfo(type);

            // Values that System.Text.Json doesn't write as a set of members, like scalars or types with a
            // custom converter, have nothing for the member access strategy to filter.
            if (typeInfo.Kind != JsonTypeInfoKind.Object)
            {
                JsonSerializer.Serialize(writer, obj, typeInfo);
                return;
            }

            if (context == null)
            {
                throw new InvalidOperationException("A TemplateContext is required to serialize FluidValue instances. Please wrap the FluidValue in a SerializableFluidValue.");
            }

            // Members are read like a template reads them, through the member access strategy, so the
            // output never contains a member the template couldn't access. System.Text.Json still
            // provides the names, the order and the ignored members.
            var objectValue = value as ObjectValueBase ?? new ObjectValue(obj);
            var memberNameStrategy = context.Options.MemberAccessStrategy.MemberNameStrategy;

            writer.WriteStartObject();

            foreach (var property in typeInfo.Properties)
            {
                if (property.Get == null || property.AttributeProvider is not MemberInfo member)
                {
                    continue;
                }

                var name = memberNameStrategy(member);
                var accessor = objectValue.GetMemberAccessor(name, context);

                if (accessor == null)
                {
                    continue;
                }

                if (property.ShouldSerialize != null && !property.ShouldSerialize(obj, property.Get(obj)))
                {
                    continue;
                }

                var memberValue = accessor is IAsyncMemberAccessor asyncAccessor
                    ? asyncAccessor.GetAsync(obj, name, context).GetAwaiter().GetResult()
                    : accessor.Get(obj, name, context);

                var fluidValue = FluidValue.Create(memberValue, context.Options);

                if (fluidValue.IsNil() && options.DefaultIgnoreCondition == JsonIgnoreCondition.WhenWritingNull)
                {
                    continue;
                }

                writer.WritePropertyName(property.Name);
                JsonSerializer.Serialize<FluidValue>(writer, new SerializableFluidValue(fluidValue, context), options);
            }

            writer.WriteEndObject();
        }
    }
}
