using BenchmarkDotNet.Attributes;
using Fluid.Parser;

namespace Fluid.Benchmarks
{
    [MemoryDiagnoser]
    public class FluidBenchmarks : BaseBenchmarks
    {
        private IFluidParser _parser  = new FluidParser();
        private IFluidTemplate _fluidTemplate;

        public FluidBenchmarks()
        {
            TemplateContext.GlobalMemberAccessStrategy.MemberNameStrategy = MemberNameStrategies.CamelCase;
            TemplateContext.GlobalMemberAccessStrategy.Register<Product>();
            _parser.TryParse(TextTemplate, out _fluidTemplate, out var errors);
        }

        [Benchmark]
        public override object Parse()
        {
            _parser.TryParse(TextTemplate, out var template, out var errors);
            return template;
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
            _parser.TryParse(TextTemplate, out var template);
            var context = new TemplateContext().SetValue("products", Products);
            return template.Render(context);
        }
    }
}
