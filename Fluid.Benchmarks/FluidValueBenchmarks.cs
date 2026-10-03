using System.Collections.Generic;
using System.Text.Json.Nodes;
using BenchmarkDotNet.Attributes;
using Fluid.Values;

namespace Fluid.Benchmarks;

[MemoryDiagnoser]
public class FluidValueBenchmarks
{
    // many interfaces, none of which is dictionary
    private static readonly List<string> nonDictionaryType = new();

    private static readonly TemplateOptions options = TemplateOptions.Default;
    private static readonly JsonValue jsonString = JsonValue.Create("2020-05-18T02:13:09+00:00");
    private static readonly JsonValue objectJsonString = JsonValue.Create<object>("2020-05-18T02:13:09+00:00");
    private static readonly JsonNode parsedJsonString = JsonNode.Parse("\"2020-05-18T02:13:09+00:00\"");

    [Benchmark]
    public FluidValue CreateFromList()
    {
        return FluidValue.Create(nonDictionaryType, options);
    }

    [Benchmark]
    public FluidValue CreateFromJsonString() => FluidValue.Create(jsonString, options);

    [Benchmark]
    public FluidValue CreateFromJsonObjectString() => FluidValue.Create(objectJsonString, options);

    [Benchmark]
    public FluidValue CreateFromJsonParsedString() => FluidValue.Create(parsedJsonString, options);
}
