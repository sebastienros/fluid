using BenchmarkDotNet.Attributes;
using Scriban;
using Scriban.Runtime;

namespace Fluid.Benchmarks
{
    [MemoryDiagnoser]
    public class ScribanBenchmarks : BaseBenchmarks
    {
        private Template _scribanTemplate;
        private ScriptObject _scriptObject;

        public ScribanBenchmarks()
        {
            _scribanTemplate = Template.ParseLiquid(TextTemplate);
            _scriptObject = new ScriptObject { { "products", Products } };
        }

        [Benchmark]
        public override object Parse()
        {
            return _scribanTemplate = Template.ParseLiquid(TextTemplate);
        }

        [Benchmark]
        public override string Render()
        {
            return _scribanTemplate.Render(_scriptObject);
        }
    }
}
