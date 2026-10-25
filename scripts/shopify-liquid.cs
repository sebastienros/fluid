#!/usr/bin/env dotnet
#:property TargetFramework=net10.0
#:property Nullable=enable
#:property PublishAot=false
#:project ../Fluid/Fluid.csproj

// Renders a Liquid template with Shopify's official Liquid engine, using the playground
// that backs the examples on https://shopify.dev/docs/api/liquid, and optionally compares
// the result with Fluid (built from this repository) or with an expected output.
//
// Run `dotnet run scripts/shopify-liquid.cs -- --help` for usage.

using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Nodes;
using Fluid;

const string Endpoint = "https://shopify.dev/liquid/render";

const string Usage = """
    Renders a Liquid template with Shopify's official Liquid engine (the shopify.dev playground).

    Usage:
      dotnet run scripts/shopify-liquid.cs -- [<template-file>] [options]

    Template source (exactly one; stdin is used when none is given and input is redirected):
      <template-file>          Path of a Liquid template file. Use - for stdin.
      -e, --inline <source>    Liquid source provided inline.

    Options:
      -p, --path <path>        Storefront page the template is rendered on. It decides which
                               objects exist (e.g. /products/health-potion exposes `product`).
                               Default: /
      --show-data              Also print, on stderr, the JSON of the store objects the template used.
      -c, --compare            Also render with Fluid (this repository) and diff the two outputs.
      -x, --expected <text>    Compare Shopify's output with this text.
      -f, --expected-file <p>  Compare Shopify's output with the content of this file.
      --json                   Print a JSON object instead of the raw output.
      -h, --help               Show this help.

    Output: Shopify's rendered output is written to stdout as-is (no trailing newline added).

    Exit codes:
      0  rendered (and matched, when comparing)
      1  outputs differ
      2  Shopify reported the template as invalid
      3  usage, network or protocol error

    Notes:
      The playground renders against a demo store. Custom data cannot be supplied: use
      {% assign %} / {% capture %} in the template, or the store objects reachable from --path.
    """;

string? templateFile = null, inline = null, expected = null, expectedFile = null;
var path = "/";
bool showData = false, compare = false, json = false;

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
            case "-p" or "--path": path = Next(); break;
            case "-x" or "--expected": expected = Next(); break;
            case "-f" or "--expected-file": expectedFile = Next(); break;
            case "-c" or "--compare": compare = true; break;
            case "--show-data": showData = true; break;
            case "--json": json = true; break;
            case "-": templateFile = "-"; break;
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
if (expected != null && expectedFile != null) return Fail("Provide either --expected or --expected-file, not both");
if (compare && (expected != null || expectedFile != null)) return Fail("--compare cannot be combined with --expected");

string source;
if (inline != null) source = inline;
else if (templateFile == "-" || (templateFile == null && Console.IsInputRedirected)) source = Console.In.ReadToEnd();
else if (templateFile != null) source = File.ReadAllText(templateFile);
else return Fail("No template provided. Use --help for usage.");

if (expectedFile != null) expected = File.ReadAllText(expectedFile);

// Rendering

ShopifyResult shopify;
try
{
    shopify = await RenderWithShopifyAsync(source, path);
}
catch (Exception ex) when (ex is HttpRequestException or TaskCanceledException or JsonException or InvalidOperationException)
{
    return Fail($"Shopify render failed: {ex.Message}");
}

string? fluid = null, fluidError = null;
if (compare)
{
    var parser = new FluidParser();
    if (parser.TryParse(source, out var template, out var error))
    {
        try { fluid = await template.RenderAsync(new TemplateContext()); }
        catch (Exception ex) { fluidError = ex.Message; }
    }
    else
    {
        fluidError = error;
    }
}

var reference = compare ? fluid : expected;
var comparing = compare || expected != null;
var matches = comparing && shopify.Valid && reference != null && reference == shopify.Output;

// Reporting

if (json)
{
    var result = new JsonObject
    {
        ["valid"] = shopify.Valid,
        ["output"] = shopify.Output,
        ["data"] = shopify.Data,
    };
    if (compare) { result["fluid"] = fluid; result["fluidError"] = fluidError; }
    if (expected != null) result["expected"] = expected;
    if (comparing) result["match"] = matches;
    Console.Out.Write(result.ToJsonString(new JsonSerializerOptions { WriteIndented = true }));
    Console.Out.WriteLine();
}
else
{
    if (showData && shopify.Data != null) Console.Error.WriteLine($"data: {shopify.Data.ToJsonString()}");

    if (!shopify.Valid)
    {
        Console.Error.WriteLine($"Shopify: invalid template ({shopify.Output})");
    }
    else if (!comparing)
    {
        Console.Out.Write(shopify.Output);
    }
    else
    {
        var label = compare ? "fluid" : "expected";
        if (compare && fluidError != null) Console.Error.WriteLine($"Fluid error: {fluidError}");

        if (matches)
        {
            Console.Out.WriteLine($"MATCH ({label} == shopify)");
            Console.Out.WriteLine(Show(shopify.Output));
        }
        else
        {
            Console.Out.WriteLine($"DIFF ({label} != shopify)");
            Console.Out.WriteLine($"shopify:  {Show(shopify.Output)}");
            Console.Out.WriteLine($"{label + ":",-9} {(reference == null ? "<none>" : Show(reference))}");
            if (reference != null) Console.Out.WriteLine($"first difference at index {FirstDifference(shopify.Output, reference)}");
        }
    }
}

if (!shopify.Valid) return 2;
return comparing && !matches ? 1 : 0;

static async Task<ShopifyResult> RenderWithShopifyAsync(string source, string path)
{
    using var http = new HttpClient { Timeout = TimeSpan.FromSeconds(30) };
    http.DefaultRequestHeaders.UserAgent.ParseAdd("fluid-shopify-liquid-check/1.0 (+https://github.com/sebastienros/fluid)");

    using var response = await http.PostAsJsonAsync(Endpoint, new { path, liquid = source });
    var body = await response.Content.ReadAsStringAsync();
    var node = JsonNode.Parse(body) as JsonObject ?? throw new InvalidOperationException($"Unexpected response: {body}");

    if (node["errors"] is JsonArray errors)
    {
        throw new InvalidOperationException(string.Join("; ", errors.Select(e => e?["message"]?.GetValue<string>())));
    }

    if (!response.IsSuccessStatusCode || node["result"] is not JsonValue result)
    {
        throw new InvalidOperationException($"HTTP {(int)response.StatusCode}: {body}");
    }

    // The playground HTML-encodes the whole rendered output once, decode it to get the raw result.
    var output = WebUtility.HtmlDecode(result.GetValue<string>());
    var valid = node["templateValid"]?.GetValue<bool>() ?? true;
    var data = node["data"] is JsonValue d && d.TryGetValue<string>(out var s) ? JsonNode.Parse(s) : null;

    return new ShopifyResult(output, valid, data);
}

static int Fail(string message)
{
    Console.Error.WriteLine(message);
    return 3;
}

// Makes whitespace visible so differences in trimming are obvious.
static string Show(string value) => JsonSerializer.Serialize(value, new JsonSerializerOptions { Encoder = System.Text.Encodings.Web.JavaScriptEncoder.UnsafeRelaxedJsonEscaping });

static int FirstDifference(string a, string b)
{
    var length = Math.Min(a.Length, b.Length);
    for (var i = 0; i < length; i++)
    {
        if (a[i] != b[i]) return i;
    }
    return length;
}

record ShopifyResult(string Output, bool Valid, JsonNode? Data);
