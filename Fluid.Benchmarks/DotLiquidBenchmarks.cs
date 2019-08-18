using BenchmarkDotNet.Attributes;
using DotLiquid;
using System.Linq;

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
            _products = MakeProducts();
        }

        private Hash MakeProducts()
        {
            return Hash.FromAnonymousObject(new
            {
                products = Products.Select(product => new Hash()
                {
                    ["name"] = product.Name,
                    ["price"] = product.Price,
                    ["description"] = product.Description
                }).ToList()
            });
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
            var products = MakeProducts();
            return template.Render(products);
        }
    }
}
