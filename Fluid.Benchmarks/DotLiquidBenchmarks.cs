using BenchmarkDotNet.Attributes;
using DotLiquid;

namespace Fluid.Benchmarks
{
    [MemoryDiagnoser]
    public class DotLiquidBenchmarks : BaseBenchmarks
    {
        
        private Template _dotLiquidTemplate;
        private Hash _products;

        public DotLiquidBenchmarks()
        {
            _dotLiquidTemplate = Template.Parse(TextTemplate);
            _dotLiquidTemplate.MakeThreadSafe();
            _products = Hash.FromAnonymousObject(new { products = Products });
        }

        [Benchmark]
        public override object Parse()
        {
            var template = Template.Parse(TextTemplate);
            return template;
        }

        [Benchmark]
        public override string Render()
        {
            return _dotLiquidTemplate.Render(_products);
        }

        [Benchmark]
        public override string ParseAndRender()
        {
            var template = Template.Parse(TextTemplate);
            var products = Hash.FromAnonymousObject(new { products = Products });
            return template.Render(products);
        }
    }
}
