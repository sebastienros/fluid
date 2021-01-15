using Scriban;
using Scriban.Runtime;

namespace Fluid.Benchmarks
{
    public class ScribanBenchmarks : BaseBenchmarks
    {
        private Template _scribanTemplate;

        public ScribanBenchmarks()
        {
            _scribanTemplate = Template.ParseLiquid(ProductTemplate);
        }

        public override object Parse()
        {
            return _scribanTemplate = Template.ParseLiquid(ProductTemplate);
        }

        public override object ParseBig()
        {
            return _scribanTemplate = Template.ParseLiquid(BlogPostTemplate);
        }

        public override string Render()
        {
            var scriptObject = new ScriptObject { { "products", Products } };
            return _scribanTemplate.Render(scriptObject);
        }

        public override string ParseAndRender()
        {
            var template = Template.ParseLiquid(ProductTemplate);
            var scriptObject = new ScriptObject { { "products", Products } };
            return template.Render(scriptObject);
        }
    }
}
