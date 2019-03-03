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
        private Liquid.NET.TemplateContext _context;

        public LiquidNetBenchmarks()
        {
            _liquidNetTemplate = LiquidTemplate.Create(TextTemplate);
            _products = Products.ToLiquid();
            _context = new Liquid.NET.TemplateContext();
            _context.DefineLocalVariable("products", _products);
        }

        [Benchmark]
        public override object Parse()
        {
            return LiquidTemplate.Create(TextTemplate);
        }

        [Benchmark]
        public override string Render()
        {
            return _liquidNetTemplate.LiquidTemplate.Render(_context).Result;
        }

        [Benchmark]
        public override string ParseAndRender()
        {
            var template = LiquidTemplate.Create(TextTemplate);
            var products = Products.ToLiquid();
            var context = new Liquid.NET.TemplateContext();
            context.DefineLocalVariable("products", products);
            return template.LiquidTemplate.Render(context).Result;
        }
    }
}
