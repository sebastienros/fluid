using DotLiquid;
using System.Linq;

namespace Fluid.Benchmarks
{
    public class DotLiquidBenchmarks : BaseBenchmarks
    {
        private Template _dotLiquidTemplate;

        public DotLiquidBenchmarks()
        {
            _dotLiquidTemplate = Template.Parse(TextTemplate);
            _dotLiquidTemplate.MakeThreadSafe();
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

        public override object Parse()
        {
            var template = Template.Parse(TextTemplate);
            return template;
        }

        public override string Render()
        {
            var products = MakeProducts();
            return _dotLiquidTemplate.Render(products);
        }

        public override string ParseAndRender()
        {
            var template = Template.Parse(TextTemplate);
            var products = MakeProducts();
            return template.Render(products);
        }
    }
}
