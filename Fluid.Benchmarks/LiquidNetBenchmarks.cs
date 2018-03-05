using System.Threading.Tasks;
using BenchmarkDotNet.Attributes;
using BenchmarkDotNet.Attributes.Jobs;
using Liquid.NET;
using Liquid.NET.Constants;
using Liquid.NET.Utils;

namespace Fluid.Benchmarks
{
    [MemoryDiagnoser]
    [ShortRunJob]
    public class LiquidNetBenchmarks : BaseBenchmarks
    {
        private LiquidParsingResult _sampleTemplateLiquidNet;

        public LiquidNetBenchmarks()
        {
            _sampleTemplateLiquidNet = LiquidTemplate.Create(_source1);

        }


        [Benchmark]
        public override object ParseSample()
        {
            return LiquidTemplate.Create(_source1);
        }

        [Benchmark]
        public override Task<string> ParseAndRenderSample()
        {
            var context = new Liquid.NET.TemplateContext();
            context.DefineLocalVariable("products", _products.ToLiquid());
            var parsingResult = LiquidTemplate.Create(_source1);
            return Task.FromResult(parsingResult.LiquidTemplate.Render(context).Result);
        }

        [Benchmark]
        public override string RenderSample()
        {
            var context = new Liquid.NET.TemplateContext();
            context.DefineLocalVariable("products", _products.ToLiquid());			
            return _sampleTemplateLiquidNet.LiquidTemplate.Render(context).Result;
        }

        [Benchmark]
        public override object ParseLoremIpsum()
        {
            return LiquidTemplate.Create(_source3);
        }

        [Benchmark]
        public override Task<string> RenderSimpleOuput()
        {
            var context = new Liquid.NET.TemplateContext();
            context.DefineLocalVariable("image", LiquidString.Create("kitten.jpg"));
            var parsingResult = LiquidTemplate.Create(_source2);
            return Task.FromResult(parsingResult.LiquidTemplate.Render(context).Result);
        }
        
        [Benchmark]
        public override Task<string> RenderLoremSimpleOuput()
        {
            var context = new Liquid.NET.TemplateContext();
            context.DefineLocalVariable("image", LiquidString.Create("kitten.jpg"));
            var parsingResult = LiquidTemplate.Create(_source4);
            return Task.FromResult(parsingResult.LiquidTemplate.Render(context).Result);
        }
    }
}
