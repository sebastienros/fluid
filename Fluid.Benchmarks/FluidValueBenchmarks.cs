using System.Collections.Generic;
using BenchmarkDotNet.Attributes;
using Fluid.Values;

namespace Fluid.Benchmarks
{
    [MemoryDiagnoser]
    public class FluidValueBenchmarks
    {
        // many interfaces, none of which is dictionary
        private static readonly List<string> nonDictionaryType = new();

        private static readonly TemplateOptions options = new();

        [Benchmark]
        public FluidValue CreateFromList()
        {
            return FluidValue.Create(nonDictionaryType, options);
        }
    }
}