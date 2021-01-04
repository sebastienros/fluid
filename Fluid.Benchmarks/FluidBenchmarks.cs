using BenchmarkDotNet.Attributes;
using Fluid.Parlot;

namespace Fluid.Benchmarks
{
    [MemoryDiagnoser]
    public class FluidBenchmarks : BaseBenchmarks
    {
        private IFluidParser _parser  = new ParlotParser();
        private IFluidTemplate _fluidTemplate;
        private TemplateContext _context;

        public FluidBenchmarks()
        {
            TemplateContext.GlobalMemberAccessStrategy.MemberNameStrategy = MemberNameStrategies.CamelCase;
            TemplateContext.GlobalMemberAccessStrategy.Register<Product>();
            _parser.TryParse(TextTemplate, out _fluidTemplate, out var errors);
            _context = new TemplateContext().SetValue("products", Products);
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
            return _fluidTemplate.Render(_context);
        }

        [Benchmark]
        public override string ParseAndRender()
        {
            _parser.TryParse(TextTemplate, out var template, out var errors);
            var context = new TemplateContext()
                .SetValue("products", Products);

            return template.Render(context);
        }
    }
}
