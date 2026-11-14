using System.IO.Compression;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace Fluid.Playground;

/// <summary>A partial template, loaded by <c>include</c>, <c>render</c> and <c>from</c> with its name.</summary>
public sealed record PlaygroundPartial(
    [property: JsonPropertyName("n")] string Name,
    [property: JsonPropertyName("s")] string Source);

/// <summary>
/// Everything the playground renders: the template, its partials, the JSON model and the options. It is stored in the
/// URL fragment (<see cref="ToHash"/>), so that a link shows the same result.
/// </summary>
public sealed record PlaygroundState
{
    /// <summary>The source of the template.</summary>
    [JsonPropertyName("t")] public string Template { get; init; } = "";

    /// <summary>The model, as JSON.</summary>
    [JsonPropertyName("m")] public string Model { get; init; } = "{}";

    /// <summary>The partial templates.</summary>
    [JsonPropertyName("p")] public IReadOnlyList<PlaygroundPartial> Partials { get; init; } = [];

    /// <summary>The options.</summary>
    [JsonPropertyName("o")] public PlaygroundOptions Options { get; init; } = new();

    /// <summary>
    /// The state as a URL fragment, without the <c>#</c>: the JSON of the state, compressed with Deflate and encoded
    /// in base64url. Options that keep their default value are not written.
    /// </summary>
    public string ToHash()
    {
        var json = JsonSerializer.SerializeToUtf8Bytes(this, PlaygroundJsonContext.Default.PlaygroundState);
        using var buffer = new MemoryStream();
        using (var deflate = new DeflateStream(buffer, CompressionLevel.SmallestSize, leaveOpen: true))
        {
            deflate.Write(json);
        }

        return Convert.ToBase64String(buffer.GetBuffer(), 0, (int)buffer.Length).TrimEnd('=').Replace('+', '-').Replace('/', '_');
    }

    /// <summary>
    /// Reads a state written by <see cref="ToHash"/>, with or without its leading <c>#</c>. Returns <see langword="null"/>
    /// for an empty or invalid fragment.
    /// </summary>
    public static PlaygroundState? FromHash(string? hash)
    {
        if (string.IsNullOrEmpty(hash))
        {
            return null;
        }

        var text = hash[0] == '#' ? hash[1..] : hash;
        if (text.Length == 0)
        {
            return null;
        }

        try
        {
            var base64 = new StringBuilder(text.Length + 3).Append(text).Replace('-', '+').Replace('_', '/');
            base64.Append('=', (4 - base64.Length % 4) % 4);
            using var deflate = new DeflateStream(new MemoryStream(Convert.FromBase64String(base64.ToString())), CompressionMode.Decompress);
            return JsonSerializer.Deserialize(deflate, PlaygroundJsonContext.Default.PlaygroundState);
        }
        catch (Exception e) when (e is FormatException or InvalidDataException or JsonException)
        {
            return null;
        }
    }
}

[JsonSourceGenerationOptions(DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingDefault)]
[JsonSerializable(typeof(PlaygroundState))]
internal sealed partial class PlaygroundJsonContext : JsonSerializerContext;
