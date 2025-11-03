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
    public class FluidValueJsonConverter : JsonConverter<FluidValue>
    {
        private readonly TemplateContext _context;

        public FluidValueJsonConverter(TemplateContext context)
        {
            _context = context ?? throw new ArgumentNullException(nameof(context));
        }

        public override FluidValue Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
        {
            throw new NotSupportedException("Deserialization of FluidValue is not supported.");
        }

        public override void Write(Utf8JsonWriter writer, FluidValue value, JsonSerializerOptions options)
        {
            switch (value)
            {
                case ArrayValue arrayValue:
                    WriteArrayValue(writer, arrayValue, options);
                    break;
                case BooleanValue booleanValue:
                    writer.WriteBooleanValue(booleanValue.ToBooleanValue());
                    break;
                case NilValue:
                    writer.WriteNullValue();
                    break;
                case NumberValue numberValue:
                    writer.WriteNumberValue(numberValue.ToNumberValue());
                    break;
                case DictionaryValue dictionaryValue:
                    WriteDictionaryValue(writer, dictionaryValue, options);
                    break;
                case ObjectValue objectValue:
                    WriteObjectValue(writer, objectValue, options);
                    break;
                case DateTimeValue dateTimeValue:
                    writer.WriteStringValue((DateTimeOffset)dateTimeValue.ToObjectValue());
                    break;
                case StringValue stringValue:
                    writer.WriteStringValue(stringValue.ToStringValue());
                    break;
                case BlankValue:
                    writer.WriteStringValue(string.Empty);
                    break;
                case EmptyValue:
                    writer.WriteStringValue(string.Empty);
                    break;
                default:
                    writer.WriteNullValue();
                    break;
            }
        }

        private void WriteArrayValue(Utf8JsonWriter writer, ArrayValue value, JsonSerializerOptions options)
        {
            writer.WriteStartArray();
            foreach (var item in value.Enumerate(_context))
            {
                // Use FluidValue type to ensure our converter is used for FluidValue items
                if (item is FluidValue fluidValue)
                {
                    JsonSerializer.Serialize<FluidValue>(writer, fluidValue, options);
                }
                else
                {
                    JsonSerializer.Serialize(writer, item, item.GetType(), options);
                }
            }
            writer.WriteEndArray();
        }

        private static void WriteDictionaryValue(Utf8JsonWriter writer, DictionaryValue value, JsonSerializerOptions options)
        {
            if (value.ToObjectValue() is IFluidIndexable dic)
            {
                writer.WriteStartObject();
                foreach (var key in dic.Keys)
                {
                    writer.WritePropertyName(key);
                    if (dic.TryGetValue(key, out var property))
                    {
                        JsonSerializer.Serialize(writer, property, options);
                    }
                    else
                    {
                        JsonSerializer.Serialize(writer, null, options);
                    }
                }

                writer.WriteEndObject();
            }
            else
            {
                writer.WriteNullValue();
            }

        }

        private static void WriteObjectValue(Utf8JsonWriter writer, ObjectValue value, JsonSerializerOptions options)
        {
            var obj = value.ToObjectValue();
            if (obj != null)
            {
                JsonSerializer.Serialize(writer, obj, obj.GetType(), options);
            }
            else
            {
                writer.WriteNullValue();
            }
        }
    }
}
