using System.Collections;
using BenchmarkDotNet.Attributes;
using Fluid.Values;

namespace Fluid.Benchmarks
{
    [MemoryDiagnoser]
    public class DictionaryKeyBenchmarks
    {
        private readonly DictionaryDictionaryFluidIndexable _dictionary = new(
            new Hashtable { ["one"] = "value", ["two"] = "value", ["three"] = "value" },
            new TemplateOptions());

        [Benchmark]
        public bool ExistingStringKey() => _dictionary.TryGetValue("two", out _);

        [Benchmark]
        public bool MissingStringKey() => _dictionary.TryGetValue("missing", out _);
    }
}
