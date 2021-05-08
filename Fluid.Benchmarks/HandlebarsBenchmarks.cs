using HandlebarsDotNet;
using System;

namespace Fluid.Benchmarks
{
    public class HandlebarsBenchmarks : BaseBenchmarks
    {
        private readonly HandlebarsTemplate<object, object> _handlebarsTemplate;

        public HandlebarsBenchmarks()
        {
            _handlebarsTemplate = Handlebars.Compile(ProductTemplateMustache);
        }

        public override object Parse()
        {
            return Handlebars.Compile(ProductTemplateMustache);
        }

        public override object ParseBig()
        {
            throw new NotSupportedException();
        }

        public override string Render()
        {
            return _handlebarsTemplate(new
            {
                products = Products
            });
        }

        public override string ParseAndRender()
        {
            var template = Handlebars.Compile(ProductTemplateMustache);
            return template(new { products = Products });
        }
    }
}
