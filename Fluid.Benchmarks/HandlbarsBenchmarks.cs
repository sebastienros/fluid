using HandlebarsDotNet;

namespace Fluid.Benchmarks
{
    public class HandlbarsBenchmarks : BaseBenchmarks
    {
        private HandlebarsTemplate<object, object> _handlbarsTemplate;

        public HandlbarsBenchmarks()
        {
            _handlbarsTemplate = Handlebars.Compile(ProductTemplate);
        }

        public override object Parse()
        {
            return _handlbarsTemplate = Handlebars.Compile(ProductTemplate);
        }

        public override object ParseBig()
        {
            return _handlbarsTemplate = Handlebars.Compile(BlogPostTemplate);
        }

        public override string Render()
        {
            return _handlbarsTemplate(new { products = Products });
        }

        public override string ParseAndRender()
        {
            var template = Handlebars.Compile(ProductTemplate);

            return _handlbarsTemplate(new { products = Products });
        }
    }
}
