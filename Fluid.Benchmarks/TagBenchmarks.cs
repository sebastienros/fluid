using System.Runtime.CompilerServices;
using BenchmarkDotNet.Attributes;

namespace Fluid.Benchmarks
{
    [MemoryDiagnoser]
    public class TagBenchmarks
    {
        private static readonly FluidParser _parser = new();
        private readonly TemplateContext _context;
        private readonly TestCase _rawTag;
        private readonly TestCase _ifWithAnds;
        private readonly TestCase _ifWithOrs;
        private readonly TestCase _elseIf;

        public TagBenchmarks()
        {
            _rawTag = new TestCase("before {% raw %} {{ TEST 3 }} {% endraw %} after");
            _ifWithAnds = new TestCase("{% if true and false and false == false %}HIDDEN{% endif %}");
            _ifWithOrs = new TestCase("{% if true == false or false or false %}HIDDEN{% endif %}");
            _elseIf = new TestCase("{% if false %}{% elsif true == false or false or false %}HIDDEN{% endif %}");

            _context = new TemplateContext();
        }

        [Benchmark]
        public object RawTag_Parse()
        {
            return _rawTag.Parse();
        }

        [Benchmark]
        public string RawTag_Render()
        {
            return _rawTag.Render(_context);
        }

        [Benchmark]
        public object IfStatement_Ands_Parse()
        {
            return _ifWithAnds.Parse();
        }

        [Benchmark]
        public string IfStatement_Ands_Render()
        {
            return _ifWithAnds.Render(_context);
        }

        [Benchmark]
        public object IfStatement_Ors_Parse()
        {
            return _ifWithOrs.Parse();
        }

        [Benchmark]
        public string IfStatement_Ors_Render()
        {
            return _ifWithOrs.Render(_context);
        }

        [Benchmark]
        public object ElseIfStatement_Parse()
        {
            return _elseIf.Parse();
        }

        [Benchmark]
        public string ElseIfStatement_Render()
        {
            return _elseIf.Render(_context);
        }

        private sealed class TestCase
        {
            private readonly string _source;
            private readonly IFluidTemplate _template;

            public TestCase(string source)
            {
                _source = source;
                _template = _parser.Parse(source);
            }

            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            public string Render(TemplateContext context) => _template.Render(context);

            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            public IFluidTemplate Parse() => _parser.Parse(_source);
        }
    }
}