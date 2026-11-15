#:property TargetFramework=net10.0
#:property Nullable=enable
#:property PublishAot=false
#:project ../../Fluid/Fluid.csproj

// Compares the output of the Liquid gem, written as JSON by render.rb, with Fluid's or with an expected output.
// Called by liquid-gem.sh and liquid-gem.ps1 for --compare and --expected, with the same environment variables
// as render.rb: LIQUID_TEMPLATE, LIQUID_DATA and LIQUID_TZ, and LIQUID_EXPECTED for --expected.
//
// Exit codes: 0 match, 1 differ, 3 usage error.

using System.Globalization;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.RegularExpressions;
using Fluid;
using Fluid.Values;

if (args.Length != 1 || !File.Exists(args[0]))
{
    Console.Error.WriteLine("Usage: dotnet run compare.cs -- <gem-result.json>");
    return 3;
}

var gem = JsonNode.Parse(File.ReadAllText(args[0])) as JsonObject;
if (gem == null)
{
    Console.Error.WriteLine("The gem result isn't a JSON object");
    return 3;
}

var gemResult = new Result((string?)gem["output"], (string?)gem["error"], (string?)gem["errorType"]);
if (gemResult.ErrorType == "usage")
{
    Console.Error.WriteLine(gemResult.Error);
    return 3;
}

var expected = Environment.GetEnvironmentVariable("LIQUID_EXPECTED");
var label = expected != null ? "expected" : "fluid";
Result other;

if (expected != null)
{
    other = new Result(expected, null, null);
}
else
{
    try
    {
        other = await RenderWithFluidAsync();
    }
    catch (Exception e) when (e is JsonException or FormatException or TimeZoneNotFoundException or InvalidTimeZoneException)
    {
        Console.Error.WriteLine($"Invalid data or time zone: {e.Message}");
        return 3;
    }
}

// Two syntax errors match: the messages of the engines differ.
var matches = gemResult.ErrorType == "syntax"
    ? other.ErrorType == "syntax"
    : gemResult.ErrorType == other.ErrorType && gemResult.Output == other.Output && gemResult.Error == other.Error;

Console.Out.WriteLine(matches ? $"MATCH ({label} == gem)" : $"DIFF ({label} != gem)");

if (matches)
{
    Console.Out.WriteLine(gemResult.Show());
}
else
{
    Console.Out.WriteLine($"gem:      {gemResult.Show()}");
    Console.Out.WriteLine($"{label + ":",-9} {other.Show()}");

    if (gemResult.Output != null && other.Output != null)
    {
        Console.Out.WriteLine($"first difference at index {FirstDifference(gemResult.Output, other.Output)}");
    }
}

if (gemResult.ErrorType == "syntax" && other.ErrorType == "syntax")
{
    Console.Error.WriteLine($"gem:   {gemResult.Error}");
    Console.Error.WriteLine($"fluid: {other.Error}");
}

Console.Out.WriteLine($"Liquid {gem["liquid"]} on Ruby {gem["ruby"]}");

return matches ? 0 : 1;

static async Task<Result> RenderWithFluidAsync()
{
    // Like the gem, which has the {% liquid %} tag. The other opt-in syntax of FluidParserOptions isn't in the gem.
    var parser = new FluidParser(new FluidParserOptions { AllowLiquidTag = true });

    var timeZoneId = Environment.GetEnvironmentVariable("LIQUID_TZ");
    var context = new TemplateContext
    {
        TimeZone = string.IsNullOrEmpty(timeZoneId) || timeZoneId == "UTC" ? TimeZoneInfo.Utc : TimeZoneInfo.FindSystemTimeZoneById(timeZoneId),
    };

    var data = Environment.GetEnvironmentVariable("LIQUID_DATA");
    if (!string.IsNullOrWhiteSpace(data))
    {
        using var document = JsonDocument.Parse(data);
        foreach (var property in document.RootElement.EnumerateObject())
        {
            context.SetValue(property.Name, ToModel(property.Value) ?? NilValue.Instance);
        }
    }

    if (!parser.TryParse(Environment.GetEnvironmentVariable("LIQUID_TEMPLATE") ?? "", out var template, out var error))
    {
        return new Result(null, error, "syntax");
    }

    try
    {
        return new Result(await template.RenderAsync(context), null, null);
    }
    catch (Exception e)
    {
        return new Result(null, e.Message, "render");
    }
}

// The values the gem gets from JSON, with {"$time": "..."} and {"$date": "..."} for its Time and Date.
static object? ToModel(JsonElement value)
{
    switch (value.ValueKind)
    {
        case JsonValueKind.Object:
            var properties = value.EnumerateObject().ToList();

            if (properties is [{ Name: "$time" } time])
            {
                return ToTime(time.Value.GetString()!);
            }

            if (properties is [{ Name: "$date" } date])
            {
                return DateOnly.ParseExact(date.Value.GetString()!, "yyyy-MM-dd", CultureInfo.InvariantCulture);
            }

            return properties.ToDictionary(p => p.Name, p => ToModel(p.Value));

        case JsonValueKind.Array:
            return value.EnumerateArray().Select(ToModel).ToList();

        case JsonValueKind.String:
            return value.GetString();

        case JsonValueKind.Number:
            // Like Ruby, an integer or a float (a decimal keeps the scale of 1.0).
            return value.TryGetInt64(out var integer) ? integer : value.GetDecimal();

        case JsonValueKind.True:
            return true;

        case JsonValueKind.False:
            return false;

        default:
            return null;
    }
}

// Like Ruby's Time.iso8601: in the time zone of the context without an offset, UTC with Z, or with its offset.
static object ToTime(string value)
{
    if (value.EndsWith('Z'))
    {
        return DateTime.Parse(value, CultureInfo.InvariantCulture, DateTimeStyles.AdjustToUniversal | DateTimeStyles.AssumeUniversal);
    }

    if (Regex.IsMatch(value, @"[+-]\d\d:?\d\d$"))
    {
        return DateTimeOffset.Parse(value, CultureInfo.InvariantCulture);
    }

    return DateTime.Parse(value, CultureInfo.InvariantCulture, DateTimeStyles.None);
}

static int FirstDifference(string a, string b)
{
    var length = Math.Min(a.Length, b.Length);
    for (var i = 0; i < length; i++)
    {
        if (a[i] != b[i]) return i;
    }
    return length;
}

record Result(string? Output, string? Error, string? ErrorType)
{
    // Makes whitespace visible so differences in trimming are obvious.
    public string Show() => Output != null
        ? JsonSerializer.Serialize(Output, new JsonSerializerOptions { Encoder = System.Text.Encodings.Web.JavaScriptEncoder.UnsafeRelaxedJsonEscaping })
        : $"<{ErrorType} error> {Error}";
}
