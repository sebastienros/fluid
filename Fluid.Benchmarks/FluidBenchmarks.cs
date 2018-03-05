using System.Threading.Tasks;
using BenchmarkDotNet.Attributes;
using BenchmarkDotNet.Attributes.Jobs;

namespace Fluid.Benchmarks
{
    [MemoryDiagnoser]
    [ShortRunJob]
    public class FluidBenchmarks : BaseBenchmarks
    {
        private FluidTemplate _sampleTemplateFluid;

        public FluidBenchmarks()
        {
            FluidTemplate.TryParse(_source1, out _sampleTemplateFluid, out var messages);

        }

        [Benchmark]
        public override object ParseSample()
        {
            FluidTemplate.TryParse(_source1, out var template, out var messages);
            return template;
        }

        [Benchmark]
        public override  Task<string> ParseAndRenderSample()
        {
            var context = new TemplateContext();
            context.SetValue("products", _products);

            FluidTemplate.TryParse(_source1, out var template, out var messages);
            return template.RenderAsync(context);
        }

        [Benchmark]
        public override string RenderSample()
        {
            var context = new TemplateContext();
            context.SetValue("products", _products);

            return _sampleTemplateFluid.Render(context);
        }

        [Benchmark]
        public override object ParseLoremIpsum()
        {
            FluidTemplate.TryParse(_source3, out var template, out var messages);
            return template;
        }

        [Benchmark]
        public override Task<string> RenderSimpleOuput()
        {
            FluidTemplate.TryParse(_source2, out var template, out var messages);
            var context = new TemplateContext();
            context.SetValue("image", "kitten.jpg");
            return template.RenderAsync(context);
        }
        
        [Benchmark]
        public override Task<string> RenderLoremSimpleOuput()
        {
            FluidTemplate.TryParse(_source4, out var template, out var messages);
            var context = new TemplateContext();
            context.SetValue("image", "kitten.jpg");
            return template.RenderAsync(context);
        }

    }
}
