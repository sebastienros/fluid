using BenchmarkDotNet.Attributes;
using Fluid.Filters;
using Fluid.Values;
using System.Linq;
using System.Threading.Tasks;

namespace Fluid.Benchmarks
{
    [MemoryDiagnoser]
    public class ReplaceFirstLastBenchmarks
    {
        private StringValue filterInput;
        private static readonly FilterArguments filterArguments = new(
            new object[] { "a", "b" }.Select(x => FluidValue.Create(x, TemplateOptions.Default)).ToArray());
        private static readonly TemplateContext context = new();

        [Params("a a a a", "c c c c")]
        public string Input { get; set; }

        [GlobalSetup]
        public void GlobalSetup()
        {
            filterInput = new StringValue(Input);
        }

        [Benchmark(Baseline = true)]
        public ValueTask<FluidValue> ReplaceFirst_New()
        {
            return StringFilters.ReplaceFirst(filterInput, filterArguments, context);
        }

        [Benchmark]
        public ValueTask<FluidValue> ReplaceLast_Old()
        {
            return ReplaceFirst_Old(filterInput, filterArguments, context);
        }

        public static ValueTask<FluidValue> ReplaceFirst_Old(FluidValue input, FilterArguments arguments, TemplateContext context)
        {
            var value = input.ToStringValue();
            var remove = arguments.At(0).ToStringValue();
            var index = value.IndexOf(remove);

            if (index == -1)
            {
                return input;
            }

            var concat = string.Concat(value.Substring(0, index), arguments.At(1).ToStringValue(), value.Substring(index + remove.Length));
            return new StringValue(concat);
        }
    }
}
