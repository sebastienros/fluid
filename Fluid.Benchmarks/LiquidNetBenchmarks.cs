using BenchmarkDotNet.Attributes;
using Liquid.NET;
using Liquid.NET.Constants;
using Liquid.NET.Utils;

namespace Fluid.Benchmarks
{
    [MemoryDiagnoser]
    public class LiquidNetBenchmarks : BaseBenchmarks
    {
        private LiquidParsingResult _liquidNetTemplate;
        private Option<ILiquidValue> _products;

        public LiquidNetBenchmarks()
        {
            _liquidNetTemplate = LiquidTemplate.Create(TextTemplate);
            _products = Products.ToLiquid();
        }

        [Benchmark]
        public override object Parse()
        {
            return LiquidTemplate.Create(TextTemplate);
        }

        [Benchmark]
        public override string Render()
        {
            var context = new Liquid.NET.TemplateContext();
            context.DefineLocalVariable("products", _products);			
            return _liquidNetTemplate.LiquidTemplate.Render(context).Result;
        }
    }
}
