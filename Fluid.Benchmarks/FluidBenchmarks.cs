using BenchmarkDotNet.Attributes;

namespace Fluid.Benchmarks
{
    [MemoryDiagnoser]
    public class FluidBenchmarks : BaseBenchmarks
    {
        private readonly TemplateOptions _options = new TemplateOptions();
        private readonly FluidParser _parser  = new FluidParser();
        private readonly IFluidTemplate _fluidTemplate;
        private readonly FluidParser _compiledParser = new FluidParser().Compile();

        public FluidBenchmarks()
        {
            _options.MemberAccessStrategy.MemberNameStrategy = MemberNameStrategies.CamelCase;
            _options.MemberAccessStrategy.Register<Product>();
            _parser.TryParse(ProductTemplate, out _fluidTemplate, out var _);

            CheckBenchmark();
        }

        [Benchmark]
        public override object Parse()
        {
            return _parser.Parse(ProductTemplate);
        }

        [Benchmark]
        public override object ParseBig()
        {
            return _parser.Parse(BlogPostTemplate);
        }

        [Benchmark]
        public object ParseCompiled()
        {
            return _compiledParser.Parse(ProductTemplate);
        }

        [Benchmark]
        public object ParseBigCompiled()
        {
            return _compiledParser.Parse(BlogPostTemplate);
        }

        [Benchmark]
        public override string Render()
        {
            var context = new TemplateContext(_options).SetValue("products", Products);
            return _fluidTemplate.Render(context);
        }

        [Benchmark]
        public override string ParseAndRender()
        {
            _parser.TryParse(ProductTemplate, out var template);
            var context = new TemplateContext(_options).SetValue("products", Products);
            return template.Render(context);
        }
    }
}
