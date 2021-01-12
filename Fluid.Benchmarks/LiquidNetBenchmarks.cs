using Liquid.NET;
using Liquid.NET.Utils;

namespace Fluid.Benchmarks
{
    public class LiquidNetBenchmarks : BaseBenchmarks
    {
        private LiquidParsingResult _liquidNetTemplate;

        public LiquidNetBenchmarks()
        {
            _liquidNetTemplate = LiquidTemplate.Create(TextTemplate);
        }

        public override object Parse()
        {
            return LiquidTemplate.Create(TextTemplate);
        }

        public override string Render()
        {
            var context = new Liquid.NET.TemplateContext();
            context.DefineLocalVariable("products", Products.ToLiquid());
            return _liquidNetTemplate.LiquidTemplate.Render(context).Result;
        }

        public override string ParseAndRender()
        {
            var template = LiquidTemplate.Create(TextTemplate);
            var context = new Liquid.NET.TemplateContext();
            context.DefineLocalVariable("products", Products.ToLiquid());
            return template.LiquidTemplate.Render(context).Result;
        }
    }
}
