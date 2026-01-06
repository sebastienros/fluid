using BenchmarkDotNet.Attributes;

namespace Fluid.Benchmarks
{
    [MemoryDiagnoser]
    public class SourceGeneratedFluidBenchmarks : BaseBenchmarks
    {
        private readonly TemplateOptions _options = new TemplateOptions();
        private readonly IFluidTemplate _productTemplate;
        private readonly IFluidTemplate _blogPostTemplate;

        public SourceGeneratedFluidBenchmarks()
        {
            _options.ModelNamesComparer = StringComparers.CamelCase;

            // Generated from product.liquid and blogpost.liquid
            _productTemplate = SourceGeneratedTemplates.Product;
            _blogPostTemplate = SourceGeneratedTemplates.Blogpost;

            CheckBenchmark();
        }

        public override object Parse() => null;
        public override object ParseBig() => null;

        [Benchmark]
        public override string Render()
        {
            var context = new TemplateContext(_options).SetValue("products", Products);
            return _productTemplate.Render(context);
        }

        public override string ParseAndRender() => Render();

        [Benchmark]
        public string RenderBig()
        {
            var context = new TemplateContext(_options).SetValue("products", Products);
            return _blogPostTemplate.Render(context);
        }
    }
}
