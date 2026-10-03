using System.Text.Json;
using System.Text.Json.Serialization;

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
                    var items = actualValue.EnumerateAsync(context).ToEnumerable();
                    foreach (var item in items)
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
                    JsonSerializer.Serialize(writer, actualValue.ToObjectValue(), options);
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
    }
}
