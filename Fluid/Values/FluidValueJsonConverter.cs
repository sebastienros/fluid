using System.Globalization;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace Fluid.Values;

/// <summary>
/// JSON converter for all FluidValue types.
/// </summary>
/// <remarks>
/// This converter can be overridden by registering a custom JsonConverter for specific FluidValue types
/// in the JsonSerializerOptions before the json filter is called.
/// </remarks>
public sealed class FluidValueJsonConverter : JsonConverter<FluidValue>
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
                    var wrapped = SerializableFluidValue.Create(item, context);
                    JsonSerializer.Serialize<FluidValue>(writer, wrapped, options);
                }
                writer.WriteEndArray();
                break;
            case FluidValues.Boolean:
                writer.WriteBooleanValue(actualValue.ToBooleanValue(context));
                break;
            case FluidValues.Nil:
                writer.WriteNullValue();
                break;
            case FluidValues.Number:
                // Like Shopify's json filter, an integer out of the range of a long is a string, which keeps its digits
                // in JavaScript, and a float too large for a decimal has the shortest exponent form: 1e+29 for 1.0e+29.
                if (actualValue is BigNumberValue { IsIntegral: true } bigInteger)
                {
                    writer.WriteStringValue(bigInteger.ToStringValue(context));
                }
                else if (actualValue is BigNumberValue { IsInfinity: false } bigFloat)
                {
                    writer.WriteRawValue(bigFloat.ToStringValue(context).Replace(".0e", "e"), skipInputValidation: true);
                }
                else
                {
                    var number = actualValue.ToNumberValue(context);

                    if ((number > long.MaxValue || number < long.MinValue) && NumberValue.GetScale(number) == 0)
                    {
                        writer.WriteStringValue(number.ToString(CultureInfo.InvariantCulture));
                    }
                    else
                    {
                        writer.WriteNumberValue(number);
                    }
                }
                break;
            case FluidValues.Dictionary:
                if (actualValue.ToObjectValue(context) is not IFluidIndexable dict)
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
                        var wrapped = SerializableFluidValue.Create(fluidValue, context);
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
                JsonSerializer.Serialize(writer, actualValue.ToObjectValue(context), options);
                break;
            case FluidValues.DateTime:
                writer.WriteStringValue((DateTimeOffset)actualValue.ToObjectValue(context));
                break;
            case FluidValues.String:
                writer.WriteStringValue(actualValue.ToStringValue(context));
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
