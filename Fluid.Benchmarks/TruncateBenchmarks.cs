using BenchmarkDotNet.Attributes;
using Fluid.Filters;
using Fluid.Values;
using System;
using System.Linq;
using System.Threading.Tasks;

namespace Fluid.Benchmarks
{
    [MemoryDiagnoser]
    public class TruncateBenchmarks
    {
        private const string EllipsisString = "...";
        private static readonly StringValue Ellipsis = new StringValue(EllipsisString);
        private static readonly NumberValue DefaultTruncateLength = NumberValue.Create(50);
        private StringValue filterInput;
        private static readonly FilterArguments filterArguments = new(
            new object[] { 13 }.Select(x => FluidValue.Create(x, TemplateOptions.Default)).ToArray());
        private static readonly TemplateContext context = new();

        [Params("The cat came back the very next day")]
        public string Input { get; set; }

        [GlobalSetup]
        public void GlobalSetup()
        {
            filterInput = new StringValue(Input);
        }

        [Benchmark(Baseline = true)]
        public ValueTask<FluidValue> Truncate_New()
        {
            return StringFilters.Truncate(filterInput, filterArguments, context);
        }

        [Benchmark]
        public ValueTask<FluidValue> Truncate_Old()
        {
            return Truncate_Old(filterInput, filterArguments, context);
        }

        public static ValueTask<FluidValue> Truncate_Old(FluidValue input, FilterArguments arguments, TemplateContext context)
        {
            if (input.IsNil())
            {
                return StringValue.Empty;
            }

            var inputStr = input.ToStringValue();

            if (inputStr == null)
            {
                return StringValue.Empty;
            }

            var length = Convert.ToInt32(arguments.At(0).Or(DefaultTruncateLength).ToNumberValue());

            if (inputStr.Length <= length)
            {
                return input;
            }

            var ellipsisStr = arguments.At(1).Or(Ellipsis).ToStringValue();

            var l = Math.Max(0, length - ellipsisStr.Length);

            var concat = string.Concat(inputStr.Substring(0, l), ellipsisStr);
            return new StringValue(concat);
        }
    }
}
