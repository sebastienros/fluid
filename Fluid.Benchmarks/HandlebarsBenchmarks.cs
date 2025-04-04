using HandlebarsDotNet;
using System;

namespace Fluid.Benchmarks
{
    public class HandlebarsBenchmarks : BaseBenchmarks
    {
        private readonly HandlebarsTemplate<object, object> _handlebarsTemplate;

        public HandlebarsBenchmarks()
        {
            var handlebars = Handlebars.Create();

            using (handlebars.Configure())
            {
                handlebars.RegisterHelper("truncate", (output, options, context, arguments) =>
                {
                    const string ellipsisStr = "...";
                    var inputStr = options.Template();
                    var length = Convert.ToInt32(arguments.Length > 0 ? arguments[0] : 50);
                    var l = Math.Max(0, length - ellipsisStr.Length);
                    var concat = string.Concat(inputStr.AsSpan().Slice(0, l), ellipsisStr);
                    output.WriteSafeString(concat);
                });

                _handlebarsTemplate = handlebars.Compile(ProductTemplateMustache);
            }
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
