#pragma warning disable FLUID001

using BenchmarkDotNet.Attributes;
using System.IO;

namespace Fluid.Benchmarks;

[MemoryDiagnoser]
public class StatementLocationBenchmarks
{
    private FluidParser _parser;
    private string _source;

    [Params(false, true)]
    public bool TrackLocations { get; set; }

    [Params(false, true)]
    public bool Compiled { get; set; }

    [Params("product", "blogpost")]
    public string Template { get; set; }

    [GlobalSetup]
    public void Setup()
    {
        _parser = new FluidParser(new FluidParserOptions { TrackStatementLocations = TrackLocations });
        if (Compiled)
        {
            _parser.Compile();
        }

        using var stream = typeof(StatementLocationBenchmarks).Assembly.GetManifestResourceStream($"Fluid.Benchmarks.{Template}.liquid");
        using var reader = new StreamReader(stream);
        _source = reader.ReadToEnd();
    }

    [Benchmark]
    public IFluidTemplate Parse() => _parser.Parse(_source);
}
