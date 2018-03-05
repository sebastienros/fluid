using System.Threading.Tasks;
using BenchmarkDotNet.Attributes;
using BenchmarkDotNet.Attributes.Jobs;
using DotLiquid;

namespace Fluid.Benchmarks
{
    [MemoryDiagnoser]
    [ShortRunJob]
    public class DotLiquidBenchmarks : BaseBenchmarks
    {
        
        private Template _sampleTemplateDotLiquid;

        public DotLiquidBenchmarks()
        {
            _sampleTemplateDotLiquid = Template.Parse(_source1);
            _sampleTemplateDotLiquid.MakeThreadSafe();
        }

        [Benchmark]
        public override object ParseSample()
        {
            var template = Template.Parse(_source1);
            return template;
        }


        [Benchmark]
        public override Task<string> ParseAndRenderSample()
        {
            var template = Template.Parse(_source1);
            return Task.FromResult(template.Render(Hash.FromAnonymousObject(new { products = _products })));
        }

        [Benchmark]
        public override string RenderSample()
        {
            return _sampleTemplateDotLiquid.Render(Hash.FromAnonymousObject(new { products = _products }));
        }


        [Benchmark]
        public override object ParseLoremIpsum()
        {
            var template = Template.Parse(_source3);
            return template;
        }

        [Benchmark]
        public override Task<string> RenderSimpleOuput()
        {
            var template = Template.Parse(_source2);
            template.Assigns.Add("image", "kitten.jpg");
            return Task.FromResult(template.Render());
        }

        [Benchmark]
        public override Task<string> RenderLoremSimpleOuput()
        {
            var template = Template.Parse(_source4);
            template.Assigns.Add("image", "kitten.jpg");
            return Task.FromResult(template.Render());
        }

    }
}
