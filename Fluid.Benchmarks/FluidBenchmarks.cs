using System.Threading.Tasks;
using BenchmarkDotNet.Attributes;
using BenchmarkDotNet.Attributes.Jobs;
using DotLiquid;
using Liquid.NET;
using Liquid.NET.Constants;
using Liquid.NET.Utils;

namespace Fluid.Benchmarks
{
    [MemoryDiagnoser]
    [ShortRunJob]
    public class FluidBenchmarks
    {
        private const string _source1 = @"
<ul id=""products"">
  {% for product in products %}
    <li>
      <h2>{{ product.name }}</h2>
      Only {{ product.price | price }}

      {{ product.description | prettyprint | paragraph }}
    </li>
  {% endfor %}
</ul>
";

        private object[] _products = new[]
        {
            new { name = "product 1", price = 1 },
            new { name = "product 2", price = 2 },
            new { name = "product 3", price = 3 },
        };

        private const string _source2 = @"{{ image }}";

        private const string _source3 = @"
Sed ut perspiciatis unde omnis iste natus error sit voluptatem accusantium doloremque laudantium, totam rem aperiam, eaque ipsa quae ab illo inventore veritatis et quasi architecto beatae vitae dicta sunt explicabo. Nemo enim ipsam voluptatem quia voluptas sit aspernatur aut odit aut fugit, sed quia consequuntur magni dolores eos qui ratione voluptatem sequi nesciunt. Neque porro quisquam est, qui dolorem ipsum quia dolor sit amet, consectetur, adipisci velit, sed quia non numquam eius modi tempora incidunt ut labore et dolore magnam aliquam quaerat voluptatem. Ut enim ad minima veniam, quis nostrum exercitationem ullam corporis suscipit laboriosam, nisi ut aliquid ex ea commodi consequatur? Quis autem vel eum iure reprehenderit qui in ea voluptate velit esse quam nihil molestiae consequatur, vel illum qui dolorem eum fugiat quo voluptas nulla pariatur?
Sed ut perspiciatis unde omnis iste natus error sit voluptatem accusantium doloremque laudantium, totam rem aperiam, eaque ipsa quae ab illo inventore veritatis et quasi architecto beatae vitae dicta sunt explicabo. Nemo enim ipsam voluptatem quia voluptas sit aspernatur aut odit aut fugit, sed quia consequuntur magni dolores eos qui ratione voluptatem sequi nesciunt. Neque porro quisquam est, qui dolorem ipsum quia dolor sit amet, consectetur, adipisci velit, sed quia non numquam eius modi tempora incidunt ut labore et dolore magnam aliquam quaerat voluptatem. Ut enim ad minima veniam, quis nostrum exercitationem ullam corporis suscipit laboriosam, nisi ut aliquid ex ea commodi consequatur? Quis autem vel eum iure reprehenderit qui in ea voluptate velit esse quam nihil molestiae consequatur, vel illum qui dolorem eum fugiat quo voluptas nulla pariatur?
Sed ut perspiciatis unde omnis iste natus error sit voluptatem accusantium doloremque laudantium, totam rem aperiam, eaque ipsa quae ab illo inventore veritatis et quasi architecto beatae vitae dicta sunt explicabo. Nemo enim ipsam voluptatem quia voluptas sit aspernatur aut odit aut fugit, sed quia consequuntur magni dolores eos qui ratione voluptatem sequi nesciunt. Neque porro quisquam est, qui dolorem ipsum quia dolor sit amet, consectetur, adipisci velit, sed quia non numquam eius modi tempora incidunt ut labore et dolore magnam aliquam quaerat voluptatem. Ut enim ad minima veniam, quis nostrum exercitationem ullam corporis suscipit laboriosam, nisi ut aliquid ex ea commodi consequatur? Quis autem vel eum iure reprehenderit qui in ea voluptate velit esse quam nihil molestiae consequatur, vel illum qui dolorem eum fugiat quo voluptas nulla pariatur?
Sed ut perspiciatis unde omnis iste natus error sit voluptatem accusantium doloremque laudantium, totam rem aperiam, eaque ipsa quae ab illo inventore veritatis et quasi architecto beatae vitae dicta sunt explicabo. Nemo enim ipsam voluptatem quia voluptas sit aspernatur aut odit aut fugit, sed quia consequuntur magni dolores eos qui ratione voluptatem sequi nesciunt. Neque porro quisquam est, qui dolorem ipsum quia dolor sit amet, consectetur, adipisci velit, sed quia non numquam eius modi tempora incidunt ut labore et dolore magnam aliquam quaerat voluptatem. Ut enim ad minima veniam, quis nostrum exercitationem ullam corporis suscipit laboriosam, nisi ut aliquid ex ea commodi consequatur? Quis autem vel eum iure reprehenderit qui in ea voluptate velit esse quam nihil molestiae consequatur, vel illum qui dolorem eum fugiat quo voluptas nulla pariatur?
Sed ut perspiciatis unde omnis iste natus error sit voluptatem accusantium doloremque laudantium, totam rem aperiam, eaque ipsa quae ab illo inventore veritatis et quasi architecto beatae vitae dicta sunt explicabo. Nemo enim ipsam voluptatem quia voluptas sit aspernatur aut odit aut fugit, sed quia consequuntur magni dolores eos qui ratione voluptatem sequi nesciunt. Neque porro quisquam est, qui dolorem ipsum quia dolor sit amet, consectetur, adipisci velit, sed quia non numquam eius modi tempora incidunt ut labore et dolore magnam aliquam quaerat voluptatem. Ut enim ad minima veniam, quis nostrum exercitationem ullam corporis suscipit laboriosam, nisi ut aliquid ex ea commodi consequatur? Quis autem vel eum iure reprehenderit qui in ea voluptate velit esse quam nihil molestiae consequatur, vel illum qui dolorem eum fugiat quo voluptas nulla pariatur?
Sed ut perspiciatis unde omnis iste natus error sit voluptatem accusantium doloremque laudantium, totam rem aperiam, eaque ipsa quae ab illo inventore veritatis et quasi architecto beatae vitae dicta sunt explicabo. Nemo enim ipsam voluptatem quia voluptas sit aspernatur aut odit aut fugit, sed quia consequuntur magni dolores eos qui ratione voluptatem sequi nesciunt. Neque porro quisquam est, qui dolorem ipsum quia dolor sit amet, consectetur, adipisci velit, sed quia non numquam eius modi tempora incidunt ut labore et dolore magnam aliquam quaerat voluptatem. Ut enim ad minima veniam, quis nostrum exercitationem ullam corporis suscipit laboriosam, nisi ut aliquid ex ea commodi consequatur? Quis autem vel eum iure reprehenderit qui in ea voluptate velit esse quam nihil molestiae consequatur, vel illum qui dolorem eum fugiat quo voluptas nulla pariatur?
Sed ut perspiciatis unde omnis iste natus error sit voluptatem accusantium doloremque laudantium, totam rem aperiam, eaque ipsa quae ab illo inventore veritatis et quasi architecto beatae vitae dicta sunt explicabo. Nemo enim ipsam voluptatem quia voluptas sit aspernatur aut odit aut fugit, sed quia consequuntur magni dolores eos qui ratione voluptatem sequi nesciunt. Neque porro quisquam est, qui dolorem ipsum quia dolor sit amet, consectetur, adipisci velit, sed quia non numquam eius modi tempora incidunt ut labore et dolore magnam aliquam quaerat voluptatem. Ut enim ad minima veniam, quis nostrum exercitationem ullam corporis suscipit laboriosam, nisi ut aliquid ex ea commodi consequatur? Quis autem vel eum iure reprehenderit qui in ea voluptate velit esse quam nihil molestiae consequatur, vel illum qui dolorem eum fugiat quo voluptas nulla pariatur?
Sed ut perspiciatis unde omnis iste natus error sit voluptatem accusantium doloremque laudantium, totam rem aperiam, eaque ipsa quae ab illo inventore veritatis et quasi architecto beatae vitae dicta sunt explicabo. Nemo enim ipsam voluptatem quia voluptas sit aspernatur aut odit aut fugit, sed quia consequuntur magni dolores eos qui ratione voluptatem sequi nesciunt. Neque porro quisquam est, qui dolorem ipsum quia dolor sit amet, consectetur, adipisci velit, sed quia non numquam eius modi tempora incidunt ut labore et dolore magnam aliquam quaerat voluptatem. Ut enim ad minima veniam, quis nostrum exercitationem ullam corporis suscipit laboriosam, nisi ut aliquid ex ea commodi consequatur? Quis autem vel eum iure reprehenderit qui in ea voluptate velit esse quam nihil molestiae consequatur, vel illum qui dolorem eum fugiat quo voluptas nulla pariatur?
Sed ut perspiciatis unde omnis iste natus error sit voluptatem accusantium doloremque laudantium, totam rem aperiam, eaque ipsa quae ab illo inventore veritatis et quasi architecto beatae vitae dicta sunt explicabo. Nemo enim ipsam voluptatem quia voluptas sit aspernatur aut odit aut fugit, sed quia consequuntur magni dolores eos qui ratione voluptatem sequi nesciunt. Neque porro quisquam est, qui dolorem ipsum quia dolor sit amet, consectetur, adipisci velit, sed quia non numquam eius modi tempora incidunt ut labore et dolore magnam aliquam quaerat voluptatem. Ut enim ad minima veniam, quis nostrum exercitationem ullam corporis suscipit laboriosam, nisi ut aliquid ex ea commodi consequatur? Quis autem vel eum iure reprehenderit qui in ea voluptate velit esse quam nihil molestiae consequatur, vel illum qui dolorem eum fugiat quo voluptas nulla pariatur?
";

        private const string _source4 = @"Sed ut perspiciatis unde omnis iste natus error sit voluptatem accusantium doloremque laudantium, totam rem aperiam, {{ image }} eaque ipsa quae ab illo inventore veritatis et quasi architecto beatae vitae dicta sunt explicabo. Nemo enim ipsam voluptatem quia voluptas sit aspernatur aut odit aut fugit, sed quia consequuntur magni dolores eos qui ratione voluptatem sequi nesciunt. Neque porro quisquam est, qui dolorem ipsum quia dolor sit amet, consectetur, adipisci velit, sed quia non numquam eius modi tempora incidunt ut labore et dolore magnam aliquam quaerat voluptatem. Ut enim ad minima veniam, quis nostrum exercitationem ullam corporis suscipit laboriosam, nisi ut aliquid ex ea commodi consequatur? Quis autem vel eum iure reprehenderit qui in ea voluptate velit esse quam nihil molestiae consequatur, vel illum qui dolorem eum fugiat quo voluptas nulla pariatur?";

        private FluidTemplate _sampleTemplateFluid;
        private Template _sampleTemplateDotLiquid;
		private LiquidParsingResult _sampleTemplateLiquidNet;

		public FluidBenchmarks()
        {
            FluidTemplate.TryParse(_source1, out _sampleTemplateFluid, out var messages);
            _sampleTemplateDotLiquid = Template.Parse(_source1);
            _sampleTemplateDotLiquid.MakeThreadSafe();
			_sampleTemplateLiquidNet = LiquidTemplate.Create(_source1);

		}

        [Benchmark]
        public IFluidTemplate ParseSampleFluid()
        {
            FluidTemplate.TryParse(_source1, out var template, out var messages);
            return template;
        }

        [Benchmark]
        public Template ParseSampleDotLiquid()
        {
            var template = Template.Parse(_source1);
            return template;
        }

		[Benchmark]
		public LiquidParsingResult ParseSampleLiquidNet()
		{
			return LiquidTemplate.Create(_source1);
		}

		[Benchmark]
        public Task<string> ParseAndRenderSampleFluid()
        {
            var context = new TemplateContext();
            context.SetValue("products", _products);

            FluidTemplate.TryParse(_source1, out var template, out var messages);
            return template.RenderAsync(context);
        }

        [Benchmark]
        public string ParseAndRenderSampleDotLiquid()
        {
            var template = Template.Parse(_source1);
            return template.Render(Hash.FromAnonymousObject(new { products = _products }));
        }

		[Benchmark]
		public string ParseAndRenderSampleLiquidNet()
		{
			var context = new Liquid.NET.TemplateContext();
			context.DefineLocalVariable("products", _products.ToLiquid());
			var parsingResult = LiquidTemplate.Create(_source1);
			return parsingResult.LiquidTemplate.Render(context).Result;
		}

		[Benchmark]
        public string RenderSampleFluid()
        {
            var context = new TemplateContext();
            context.SetValue("products", _products);

            return _sampleTemplateFluid.Render(context);
        }

        [Benchmark]
        public string RenderSampleDotLiquid()
        {
            return _sampleTemplateDotLiquid.Render(Hash.FromAnonymousObject(new { products = _products }));
        }

		[Benchmark]
		public string RenderSampleLiquidNet()
		{
			var context = new Liquid.NET.TemplateContext();
			context.DefineLocalVariable("products", _products.ToLiquid());			
			return _sampleTemplateLiquidNet.LiquidTemplate.Render(context).Result;
		}

		[Benchmark]
        public IFluidTemplate ParseLoremIpsumFluid()
        {
            FluidTemplate.TryParse(_source3, out var template, out var messages);
            return template;
        }

        [Benchmark]
        public Template ParseLoremIpsumDotLiquid()
        {
            var template = Template.Parse(_source3);
            return template;
        }

		[Benchmark]
		public LiquidParsingResult ParseLoremIpsumLiquidNet()
		{
			return LiquidTemplate.Create(_source3);
		}

		[Benchmark]
        public Task<string> RenderSimpleOuputFluid()
        {
            FluidTemplate.TryParse(_source2, out var template, out var messages);
            var context = new TemplateContext();
            context.SetValue("image", "kitten.jpg");
            return template.RenderAsync(context);
        }

        [Benchmark]
        public string RenderSimpleOuputDotLiquid()
        {
            var template = Template.Parse(_source2);
            template.Assigns.Add("image", "kitten.jpg");
            return template.Render();
        }

		[Benchmark]
		public string RenderSimpleOuputLiquidNet()
		{
			var context = new Liquid.NET.TemplateContext();
			context.DefineLocalVariable("image", LiquidString.Create("kitten.jpg"));
			var parsingResult = LiquidTemplate.Create(_source2);
			return parsingResult.LiquidTemplate.Render(context).Result;
		}

		[Benchmark]
        public Task<string> RenderLoremSimpleOuputFluid()
        {
            FluidTemplate.TryParse(_source4, out var template, out var messages);
            var context = new TemplateContext();
            context.SetValue("image", "kitten.jpg");
            return template.RenderAsync(context);
        }

        [Benchmark]
        public string RenderLoreSimpleOuputDotLiquid()
        {
            var template = Template.Parse(_source4);
            template.Assigns.Add("image", "kitten.jpg");
            return template.Render();
        }

		[Benchmark]
		public string RenderLoreSimpleOuputLiquidNet()
		{
			var context = new Liquid.NET.TemplateContext();
			context.DefineLocalVariable("image", LiquidString.Create("kitten.jpg"));
			var parsingResult = LiquidTemplate.Create(_source4);
			return parsingResult.LiquidTemplate.Render(context).Result;
		}
	}
}
