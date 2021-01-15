using BenchmarkDotNet.Attributes;

namespace Fluid.Benchmarks
{
    [MemoryDiagnoser]
    public class FluidBenchmarks : BaseBenchmarks
    {
        private readonly FluidParser _parser  = new FluidParser();
        private readonly IFluidTemplate _fluidTemplate;

        public FluidBenchmarks()
        {
            TemplateContext.GlobalMemberAccessStrategy.MemberNameStrategy = MemberNameStrategies.CamelCase;
            TemplateContext.GlobalMemberAccessStrategy.Register<Product>();
            _parser.TryParse(ProductTemplate, out _fluidTemplate, out var _);
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
        public override string Render()
        {
            var context = new TemplateContext().SetValue("products", Products);
            return _fluidTemplate.Render(context);
        }

        [Benchmark]
        public override string ParseAndRender()
        {
            _parser.TryParse(ProductTemplate, out var template);
            var context = new TemplateContext().SetValue("products", Products);
            return template.Render(context);
        }
    }
}
