#!/usr/bin/env dotnet
#:property TargetFramework=net10.0
#:property Nullable=enable
#:property PublishAot=false
#:project ../../Fluid/Fluid.csproj

// Renders a Liquid template with the Liquid gem (https://github.com/Shopify/liquid), the reference implementation,
// in Docker, and optionally compares the result with Fluid (built from this repository) or with an expected output.
// The image is built from the Dockerfile and render.rb next to this file.
//
// Run `dotnet run scripts/liquid-gem/liquid-gem.cs -- --help` for usage.

using System.ComponentModel;
using System.Diagnostics;
using System.Globalization;
using System.Runtime.CompilerServices;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.RegularExpressions;
using Fluid;
using Fluid.Values;

const string Usage = """
    Renders a Liquid template with the Liquid gem, the reference implementation, in Docker.

    Usage:
      dotnet run scripts/liquid-gem/liquid-gem.cs -- [<template-file>] [options]

    Template source (exactly one; stdin is used when none is given and input is redirected):
      <template-file>             Path of a Liquid template file. Use - for stdin.
      -e, --inline <source>       Liquid source provided inline.

    Options:
      -d, --data <file>           JSON file with the variables of the template.
      -j, --data-json <json>      The variables of the template, as inline JSON.
                                  {"$time": "2026-03-04T13:00:00"} is a Ruby Time (a .NET DateTime) in the time zone,
                                  or with its offset ("...-05:00", "...Z" for Time.utc), {"$date": "2026-03-04"} a Date
                                  (a DateOnly). Integers are Integer (long), other numbers Float (decimal).
      -z, --tz <zone>             Time zone, like America/New_York: TZ for Ruby, TemplateContext.TimeZone for Fluid.
                                  Default: UTC
      -m, --error-mode <mode>     The gem's error mode: lax, warn or strict. Default: strict
      -c, --compare               Also render with Fluid (this repository) and diff the two outputs.
                                  Fluid parses with AllowLiquidTag, like the gem.
      -x, --expected <text>       Compare the gem's output with this text.
      -f, --expected-file <file>  Compare the gem's output with the content of this file.
          --json                  Print the gem's result as JSON: output, error, errorType, liquid and ruby versions.
          --liquid-version <v>    Version of the Liquid gem. Default: 5.14.0
      -h, --help                  Show this help.

    Output: the gem's rendered output is written to stdout as-is, errors to stderr.

    Exit codes:
      0  rendered (and matched, when comparing)
      1  outputs differ
      2  the gem reported a syntax error
      3  usage or Docker error
      4  the gem reported a render error

    Notes:
      Needs Docker. The image (fluid-liquid-gem:<version>) is built from scripts/liquid-gem on the first run,
      then reused. Templates can't include partials.
    """;

string? templateFile = null, inline = null, data = null, expected = null;
string timeZone = "UTC", errorMode = "strict", liquidVersion = "5.14.0";
bool compare = false, json = false;

for (var i = 0; i < args.Length; i++)
{
    var arg = args[i];
    string Next() => i + 1 < args.Length ? args[++i] : throw new ArgumentException($"Missing value for {arg}");

    try
    {
        switch (arg)
        {
            case "-h" or "--help": Console.Out.Write(Usage); return 0;
            case "-e" or "--inline": inline = Next(); break;
            case "-d" or "--data": data = ReadFile(Next(), "Data"); break;
            case "-j" or "--data-json": data = Next(); break;
            case "-z" or "--tz": timeZone = Next(); break;
            case "-m" or "--error-mode": errorMode = Next(); break;
            case "-c" or "--compare": compare = true; break;
            case "-x" or "--expected": expected = Next(); break;
            case "-f" or "--expected-file": expected = ReadFile(Next(), "Expected"); break;
            case "--json": json = true; break;
            case "--liquid-version": liquidVersion = Next(); break;
            case "-": templateFile = templateFile == null ? "-" : throw new ArgumentException("Only one template file can be provided"); break;
            default:
                if (arg.StartsWith('-')) throw new ArgumentException($"Unknown option {arg}");
                if (templateFile != null) throw new ArgumentException("Only one template file can be provided");
                templateFile = arg;
                break;
        }
    }
    catch (ArgumentException ex)
    {
        return Fail(ex.Message);
    }
}

if (templateFile != null && inline != null) return Fail("Provide either a template file or --inline, not both");
if (compare && expected != null) return Fail("--compare cannot be combined with --expected");

string source;
try
{
    if (inline != null) source = inline;
    else if (templateFile == "-" || (templateFile == null && Console.IsInputRedirected)) source = ReadStdin();
    else if (templateFile != null) source = ReadFile(templateFile, "Template");
    else return Fail("No template provided. Use --help for usage.");
}
catch (ArgumentException ex)
{
    return Fail(ex.Message);
}

// Rendering with the gem

GemResult gem;
try
{
    gem = RenderWithGem(source, data, timeZone, errorMode, liquidVersion);
}
catch (InvalidOperationException ex)
{
    return Fail(ex.Message);
}

if (gem.ErrorType == "usage") return Fail(gem.Error ?? "Invalid arguments");

if (!compare && expected == null)
{
    if (json)
    {
        Console.Out.WriteLine(gem.Json);
    }
    else if (gem.Output != null)
    {
        // Exactly, whatever the encoding of the console.
        using var stdout = Console.OpenStandardOutput();
        stdout.Write(new UTF8Encoding(false).GetBytes(gem.Output));
    }
    else
    {
        Console.Error.WriteLine(gem.Error);
    }

    return gem.ErrorType switch { "syntax" => 2, "render" => 4, _ => 0 };
}

if (json) Console.Error.WriteLine(gem.Json);

// Comparing

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
        other = await RenderWithFluidAsync(source, data, timeZone);
    }
    catch (Exception e) when (e is JsonException or FormatException or TimeZoneNotFoundException or InvalidTimeZoneException)
    {
        return Fail($"Invalid data or time zone: {e.Message}");
    }
}

var gemResult = new Result(gem.Output, gem.Error, gem.ErrorType);

// Two syntax errors match: the messages of the engines differ.
var matches = gemResult.ErrorType == "syntax"
    ? other.ErrorType == "syntax"
    : gemResult == other;

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

Console.Out.WriteLine($"Liquid {gem.LiquidVersion} on Ruby {gem.RubyVersion}");

return matches ? 0 : 1;

static GemResult RenderWithGem(string source, string? data, string timeZone, string errorMode, string liquidVersion)
{
    var image = $"fluid-liquid-gem:{liquidVersion}";

    // Built on the first run, then from Docker's cache.
    var build = Run("docker", ["build", "--quiet", "--tag", image, "--build-arg", $"LIQUID_VERSION={liquidVersion}", ScriptDirectory()], null);
    if (build.ExitCode != 0)
    {
        throw new InvalidOperationException($"Building the {image} image failed:{Environment.NewLine}{build.Error}");
    }

    // The template and the data are passed in the environment, so that they need no quoting.
    var environment = new Dictionary<string, string>
    {
        ["LIQUID_TEMPLATE"] = source,
        ["LIQUID_DATA"] = data ?? "",
        ["LIQUID_ERROR_MODE"] = errorMode,
        ["LIQUID_FORMAT"] = "json",
    };

    var run = Run("docker", ["run", "--rm", "-e", "LIQUID_TEMPLATE", "-e", "LIQUID_DATA", "-e", "LIQUID_ERROR_MODE", "-e", "LIQUID_FORMAT", "-e", $"TZ={timeZone}", image], environment);

    // Docker's own failures are 125 to 127. render.rb always writes a JSON result otherwise.
    if (run.ExitCode >= 125 || JsonNode.Parse(run.Output) is not JsonObject result)
    {
        throw new InvalidOperationException($"Running the {image} image failed:{Environment.NewLine}{run.Error}{run.Output}");
    }

    return new GemResult(
        (string?)result["output"],
        (string?)result["error"],
        (string?)result["errorType"],
        (string?)result["liquid"],
        (string?)result["ruby"],
        run.Output.Trim());
}

static async Task<Result> RenderWithFluidAsync(string source, string? data, string timeZone)
{
    // Like the gem, which has the {% liquid %} tag. The other opt-in syntax of FluidParserOptions isn't in the gem.
    var parser = new FluidParser(new FluidParserOptions { AllowLiquidTag = true });

    var context = new TemplateContext
    {
        TimeZone = timeZone == "UTC" ? TimeZoneInfo.Utc : TimeZoneInfo.FindSystemTimeZoneById(timeZone),
    };

    if (!string.IsNullOrWhiteSpace(data))
    {
        using var document = JsonDocument.Parse(data);
        foreach (var property in document.RootElement.EnumerateObject())
        {
            context.SetValue(property.Name, ToModel(property.Value) ?? NilValue.Instance);
        }
    }

    if (!parser.TryParse(source, out var template, out var error))
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

static (int ExitCode, string Output, string Error) Run(string fileName, string[] arguments, Dictionary<string, string>? environment)
{
    var startInfo = new ProcessStartInfo(fileName)
    {
        RedirectStandardOutput = true,
        RedirectStandardError = true,
        StandardOutputEncoding = Encoding.UTF8,
        StandardErrorEncoding = Encoding.UTF8,
    };

    foreach (var argument in arguments) startInfo.ArgumentList.Add(argument);
    foreach (var (name, value) in environment ?? []) startInfo.Environment[name] = value;

    try
    {
        using var process = Process.Start(startInfo)!;
        var error = process.StandardError.ReadToEndAsync();
        var output = process.StandardOutput.ReadToEnd();
        process.WaitForExit();
        return (process.ExitCode, output, error.Result);
    }
    catch (Win32Exception)
    {
        throw new InvalidOperationException($"{fileName} is required: https://docs.docker.com/get-docker/");
    }
}

// The folder of this file, with the Dockerfile and render.rb.
static string ScriptDirectory([CallerFilePath] string path = "") => Path.GetDirectoryName(path)!;

// Exactly, trailing newlines included.
static string ReadFile(string path, string kind)
{
    return File.Exists(path) ? File.ReadAllText(path) : throw new ArgumentException($"{kind} file not found: {path}");
}

static string ReadStdin()
{
    using var reader = new StreamReader(Console.OpenStandardInput(), Encoding.UTF8);
    return reader.ReadToEnd();
}

static int Fail(string message)
{
    Console.Error.WriteLine(message);
    return 3;
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

record GemResult(string? Output, string? Error, string? ErrorType, string? LiquidVersion, string? RubyVersion, string Json);

record Result(string? Output, string? Error, string? ErrorType)
{
    // Makes whitespace visible so differences in trimming are obvious.
    public string Show() => Output != null
        ? JsonSerializer.Serialize(Output, new JsonSerializerOptions { Encoder = System.Text.Encodings.Web.JavaScriptEncoder.UnsafeRelaxedJsonEscaping })
        : $"<{ErrorType} error> {Error}";
}
