using Liquid.NET;
using Liquid.NET.Utils;

namespace Fluid.Benchmarks
{
    public class LiquidNetBenchmarks : BaseBenchmarks
    {
        private readonly LiquidParsingResult _liquidNetTemplate;

        public LiquidNetBenchmarks()
        {
            _liquidNetTemplate = LiquidTemplate.Create(ProductTemplate);
        }

        public override object Parse()
        {
            return LiquidTemplate.Create(ProductTemplate);
        }

        public override object ParseBig()
        {
            return LiquidTemplate.Create(BlogPostTemplate);
        }

        public override string Render()
        {
            var context = new Liquid.NET.TemplateContext();
            context.DefineLocalVariable("products", Products.ToLiquid());
            return _liquidNetTemplate.LiquidTemplate.Render(context).Result;
        }

        public override string ParseAndRender()
        {
            var template = LiquidTemplate.Create(ProductTemplate);
            var context = new Liquid.NET.TemplateContext();
            context.DefineLocalVariable("products", Products.ToLiquid());
            return template.LiquidTemplate.Render(context).Result;
        }
    }
}
