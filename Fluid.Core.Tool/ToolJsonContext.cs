using System.Text.Json.Serialization;
using Fluid.Values;

namespace Fluid.Tool;

/// <summary>
/// Source generated metadata used by the <c>json</c> filter, as reflection based serialization is disabled under AOT.
/// </summary>
[JsonSerializable(typeof(FluidValue))]
internal sealed partial class ToolJsonContext : JsonSerializerContext
{
}
