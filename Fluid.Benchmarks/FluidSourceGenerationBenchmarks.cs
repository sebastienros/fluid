using System;
using BenchmarkDotNet.Attributes;

namespace Fluid.Benchmarks
{
    // Focused comparison of render performance:
    // - runtime-parsed template (FluidParser)
    // - compile-time source-generated template ([FluidTemplates] + AdditionalFiles)
    [MemoryDiagnoser]
    public class FluidSourceGenerationBenchmarks : BaseBenchmarks
    {
        private readonly TemplateOptions _options = new TemplateOptions();

        private readonly FluidParser _parser = new FluidParser();

        private readonly IFluidTemplate _runtimeTemplate;
        private readonly IFluidTemplate _sourceGeneratedTemplate;

        private readonly IFluidTemplate _runtimeBigTemplate;
        private readonly IFluidTemplate _sourceGeneratedBigTemplate;

        public FluidSourceGenerationBenchmarks()
        {
            _options.ModelNamesComparer = StringComparers.CamelCase;

            _parser.TryParse(ProductTemplate, out _runtimeTemplate, out var _);
            _sourceGeneratedTemplate = SourceGeneratedTemplates.Product;

            _parser.TryParse(BlogPostTemplate, out _runtimeBigTemplate, out var _);
            _sourceGeneratedBigTemplate = SourceGeneratedTemplates.Blogpost;

            CheckBenchmark();
        }

        [GlobalSetup]
        public void ValidateSourceGeneratedMatchesRuntime()
        {
            var runtime = Render_RuntimeParsed();
            var generated = Render_SourceGenerated();
            if (!string.Equals(runtime, generated, StringComparison.Ordinal))
            {
                throw new InvalidOperationException("Templates don't match between runtime-parsed and source-generated versions.");
            }

            var runtimeBig = RenderBig_RuntimeParsed();
            var generatedBig = RenderBig_SourceGenerated();
            if (!string.Equals(runtimeBig, generatedBig, StringComparison.Ordinal))
            {
                throw new InvalidOperationException("Big templates don't match between runtime-parsed and source-generated versions.");
            }
        }

        private static void AssertEqual(string templateName, string expected, string actual)
        {
            if (string.Equals(expected, actual, StringComparison.Ordinal))
            {
                return;
            }

            var expectedLen = expected?.Length ?? 0;
            var actualLen = actual?.Length ?? 0;
            var min = expectedLen < actualLen ? expectedLen : actualLen;

            var diffIndex = 0;
            for (; diffIndex < min; diffIndex++)
            {
                if (expected[diffIndex] != actual[diffIndex])
                {
                    break;
                }
            }

            static string Snip(string s, int index)
            {
                if (s == null)
                {
                    return "<null>";
                }

                const int context = 60;
                var start = index - context;
                if (start < 0) start = 0;
                var len = s.Length - start;
                if (len > context * 2) len = context * 2;
                return s.Substring(start, len);
            }

            throw new InvalidOperationException(
                $"Source-generated output differs from runtime output for '{templateName}'. " +
                $"Length(runtime)={expectedLen}, Length(generated)={actualLen}, FirstDiffIndex={diffIndex}.\n" +
                $"Runtime:   '{Snip(expected, diffIndex)}'\n" +
                $"Generated: '{Snip(actual, diffIndex)}'");
        }

        public override object Parse() => _parser.Parse(ProductTemplate);
        public override object ParseBig() => _parser.Parse(BlogPostTemplate);

        // Used by BaseBenchmarks.CheckBenchmark().
        public override string Render() => Render_RuntimeParsed();
        public override string ParseAndRender() => Render_RuntimeParsed();

        [Benchmark(Baseline = true)]
        public string Render_RuntimeParsed()
        {
            var context = new TemplateContext(_options).SetValue("products", Products);
            return _runtimeTemplate.Render(context);
        }

        [Benchmark]
        public string Render_SourceGenerated()
        {
            var context = new TemplateContext(_options).SetValue("products", Products);
            return _sourceGeneratedTemplate.Render(context);
        }

        [Benchmark]
        public string RenderBig_RuntimeParsed()
        {
            var context = new TemplateContext(_options).SetValue("products", Products);
            return _runtimeBigTemplate.Render(context);
        }

        [Benchmark]
        public string RenderBig_SourceGenerated()
        {
            var context = new TemplateContext(_options).SetValue("products", Products);
            return _sourceGeneratedBigTemplate.Render(context);
        }
    }
}
