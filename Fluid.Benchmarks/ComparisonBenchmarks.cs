using BenchmarkDotNet.Attributes;
using BenchmarkDotNet.Configs;

namespace Fluid.Benchmarks
{
    [MemoryDiagnoser, GroupBenchmarksBy(BenchmarkLogicalGroupRule.ByCategory), ShortRunJob]
    public class ComparisonBenchmarks
    {
        private FluidBenchmarks _fluidBenchmarks = new FluidBenchmarks();
        private DotLiquidBenchmarks _dotLiquidBenchmarks = new DotLiquidBenchmarks();
        private LiquidNetBenchmarks _liquidNetBenchmarks = new LiquidNetBenchmarks();
        private ScribanBenchmarks _scribanBenchmarks = new ScribanBenchmarks();
        private HandlbarsBenchmarks _handlbarsBenchmarks = new HandlbarsBenchmarks();

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
        public object Handlbars_Parse()
        {
            return _handlbarsBenchmarks.Parse();
        }

        [Benchmark, BenchmarkCategory("ParseBig")]
        public object Handlbars_ParseBig()
        {
            return _handlbarsBenchmarks.ParseBig();
        }

        [Benchmark, BenchmarkCategory("Render")]
        public string Handlbars_Render()
        {
            return _handlbarsBenchmarks.Render();
        }
    }
}
