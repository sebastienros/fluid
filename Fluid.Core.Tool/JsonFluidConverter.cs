using System.Text.Json;
using Fluid.Values;

namespace Fluid.Tool;

/// <summary>
/// Converts JSON to <see cref="FluidValue"/> instances without using reflection.
/// </summary>
public static class JsonFluidConverter
{
    public static FluidValue Convert(JsonElement element)
    {
        switch (element.ValueKind)
        {
            case JsonValueKind.Object:
                var dictionary = new Dictionary<string, FluidValue>(StringComparer.Ordinal);
                foreach (var property in element.EnumerateObject())
                {
                    dictionary[property.Name] = Convert(property.Value);
                }
                return new DictionaryValue(new FluidValueDictionaryFluidIndexable(dictionary));

            case JsonValueKind.Array:
                var list = new List<FluidValue>(element.GetArrayLength());
                foreach (var item in element.EnumerateArray())
                {
                    list.Add(Convert(item));
                }
                return new ArrayValue(list);

            case JsonValueKind.String:
                return StringValue.Create(element.GetString()!);

            case JsonValueKind.Number:
                return element.TryGetInt32(out var integer)
                    ? NumberValue.Create(integer)
                    : NumberValue.Create(element.GetDecimal());

            case JsonValueKind.True:
                return BooleanValue.True;

            case JsonValueKind.False:
                return BooleanValue.False;

            default:
                return NilValue.Instance;
        }
    }
}
