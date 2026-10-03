using System.Collections.Generic;
using System.Linq;
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
        private static readonly List<int> populatedList = new() { 1, 2, 3 };
        private static readonly IEnumerable<int> enumerable = populatedList.Where(_ => true);
        private static readonly ArrayValue array = (ArrayValue)FluidValue.Create(populatedList, options);
        private static readonly TemplateContext context = new(options);

        [Benchmark]
        public FluidValue CreateFromList()
        {
            return FluidValue.Create(nonDictionaryType, options);
        }

        [Benchmark]
        public FluidValue CreateFromPopulatedList()
        {
            return FluidValue.Create(populatedList, options);
        }

        [Benchmark]
        public FluidValue CreateFromEnumerable()
        {
            return FluidValue.Create(enumerable, options);
        }

        [Benchmark]
        public FluidValue GetArraySize()
        {
            return array.GetValueAsync("size", context).Result;
        }
    }
}