using BenchmarkDotNet.Attributes;
using Fluid.Values;

namespace Fluid.Benchmarks
{
    [MemoryDiagnoser]
    public class ContextModelAccessorBenchmarks
    {
        private readonly TemplateContext _reflected;
        private readonly TemplateContext _generated;
        private readonly TemplateContext _registered;

        public ContextModelAccessorBenchmarks()
        {
            var model = new ContextBenchmarkModel();
            _reflected = new TemplateContext(model, new TemplateOptions
            {
                MemberAccessStrategy = new ReflectionOnlyStrategy()
            });
            _generated = new TemplateContext(model, new TemplateOptions());
            var registeredOptions = new TemplateOptions();
            registeredOptions.MemberAccessStrategy.Register<ContextBenchmarkModel>();
            _registered = new TemplateContext(model, registeredOptions);

            if (ReflectionFallback().Result.ToStringValue() != "value" ||
                GeneratedFallback().Result.ToStringValue() != "value" ||
                ExplicitRegistration().Result.ToStringValue() != "value")
            {
                throw new System.InvalidOperationException("Member accessor benchmark produced an unexpected value.");
            }
        }

        [Benchmark(Baseline = true)]
        public System.Threading.Tasks.ValueTask<FluidValue> ReflectionFallback()
            => _reflected.Model.GetValueAsync("Value", _reflected);

        [Benchmark]
        public System.Threading.Tasks.ValueTask<FluidValue> GeneratedFallback()
            => _generated.Model.GetValueAsync("Value", _generated);

        [Benchmark]
        public System.Threading.Tasks.ValueTask<FluidValue> ExplicitRegistration()
            => _registered.Model.GetValueAsync("Value", _registered);

        private sealed class ReflectionOnlyStrategy : DefaultMemberAccessStrategy
        {
        }
    }

    public sealed class ContextBenchmarkModel
    {
        public string Value => "value";
    }
}
