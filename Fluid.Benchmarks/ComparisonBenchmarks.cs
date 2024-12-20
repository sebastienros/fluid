using BenchmarkDotNet.Attributes;
using BenchmarkDotNet.Configs;

namespace Fluid.Benchmarks
{
    [MemoryDiagnoser, GroupBenchmarksBy(BenchmarkLogicalGroupRule.ByCategory), ShortRunJob]
    public class ComparisonBenchmarks
    {
        private readonly FluidBenchmarks _fluidBenchmarks = new FluidBenchmarks();
        private readonly HandlebarsBenchmarks _handlebarsBenchmarks = new HandlebarsBenchmarks();
        private readonly DotLiquidBenchmarks _dotLiquidBenchmarks = new DotLiquidBenchmarks();
        private readonly LiquidNetBenchmarks _liquidNetBenchmarks = new LiquidNetBenchmarks();
        private readonly ScribanBenchmarks _scribanBenchmarks = new ScribanBenchmarks();
        private readonly RazorSlicesBenchmarks _razorSlicesBenchmarks = new RazorSlicesBenchmarks();

        [Benchmark(Baseline = true), BenchmarkCategory("Parse")]
        public object Fluid_Parse()
        {
            return _fluidBenchmarks.Parse();
        }

        [Benchmark(Baseline = true), BenchmarkCategory("ParseBig")]
        public object Fluid_ParseBig()
        {
            return _fluidBenchmarks.ParseBig();
        }

        [Benchmark(Baseline = true), BenchmarkCategory("Render")]
        public string Fluid_Render()
        {
            return _fluidBenchmarks.Render();
        }

        [Benchmark, BenchmarkCategory("Parse")]
        public object Scriban_Parse()
        {
            return _scribanBenchmarks.Parse();
        }

        [Benchmark, BenchmarkCategory("ParseBig")]
        public object Scriban_ParseBig()
        {
            return _scribanBenchmarks.ParseBig();
        }

        [Benchmark, BenchmarkCategory("Render")]
        public string Scriban_Render()
        {
            return _scribanBenchmarks.Render();
        }

        [Benchmark, BenchmarkCategory("Parse")]
        public object DotLiquid_Parse()
        {
            return _dotLiquidBenchmarks.Parse();
        }

        [Benchmark, BenchmarkCategory("ParseBig")]
        public object DotLiquid_ParseBig()
        {
            return _dotLiquidBenchmarks.ParseBig();
        }

        [Benchmark, BenchmarkCategory("Render")]
        public string DotLiquid_Render()
        {
            return _dotLiquidBenchmarks.Render();
        }

        [Benchmark, BenchmarkCategory("Parse")]
        public object LiquidNet_Parse()
        {
            return _liquidNetBenchmarks.Parse();
        }

        [Benchmark, BenchmarkCategory("ParseBig")]
        public object LiquidNet_ParseBig()
        {
            return _liquidNetBenchmarks.ParseBig();
        }

        [Benchmark, BenchmarkCategory("Render")]
        public string LiquidNet_Render()
        {
            return _liquidNetBenchmarks.Render();
        }

        [Benchmark, BenchmarkCategory("Parse")]
        public object Handlebars_Parse()
        {
            return _handlebarsBenchmarks.Parse();
        }

        [Benchmark, BenchmarkCategory("Render")]
        public string Handlebars_Render()
        {
            return _handlebarsBenchmarks.Render();
        }

        [Benchmark, BenchmarkCategory("Render")]
        public string RazorSlices_Render()
        {
            return _razorSlicesBenchmarks.Render();
        }
    }
}
