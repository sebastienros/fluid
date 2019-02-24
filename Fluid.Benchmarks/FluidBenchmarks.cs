using BenchmarkDotNet.Attributes;
using System;

namespace Fluid.Benchmarks
{
    [MemoryDiagnoser]
    public class FluidBenchmarks : BaseBenchmarks
    {
        private FluidTemplate _fluidTemplate;
        private TemplateContext _context;

        public FluidBenchmarks()
        {
            FluidTemplate.TryParse(TextTemplate, out _fluidTemplate, out var errors);
            _context = new TemplateContext();
            _context.SetValue("products", Products);
        }

        [Benchmark]
        public override object Parse()
        {
            if (!FluidTemplate.TryParse(TextTemplate, false, out var template, out var errors))
            {
                throw new InvalidOperationException("Liquid template not parsed");
            }

            return template;
        }

        [Benchmark]
        public override string Render()
        {
            return _fluidTemplate.Render(_context);
        }
    }
}
