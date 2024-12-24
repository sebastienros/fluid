using HandlebarsDotNet;
using System;

namespace Fluid.Benchmarks
{
    public class HandlebarsBenchmarks : BaseBenchmarks
    {
        private readonly HandlebarsTemplate<object, TemplateModel> _handlebarsTemplate;

        public HandlebarsBenchmarks() : base()
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
            return _handlebarsTemplate(TemplateModel);
        }

        public override string ParseAndRender()
        {
            var template = Handlebars.Compile(ProductTemplateMustache);
            return template(TemplateModel);
        }
    }
}
