using BenchmarkDotNet.Attributes;
using BenchmarkDotNet.Configs;
using System;
using System.Buffers;
using System.Collections.Generic;
using System.Linq.Expressions;
using System.Reflection;
using System.Threading.Tasks;

namespace Fluid.Benchmarks
{
    [MemoryDiagnoser]
    [GroupBenchmarksBy(BenchmarkLogicalGroupRule.ByCategory)]
    [CategoriesColumn]
    [ShortRunJob]
    public class StringBufferBenchmarks
    {
        private const int MaximumSegmentCapacity = 32 * 1024;
        private static readonly Func<int, object> CreateParentBuffer =
            static capacity => new ParentBuffer(capacity);
        private static readonly Func<int, object> CreateSegmentedBuffer =
            CreateBufferFactory();
        private static readonly Scenario[] Scenarios =
        [
            Scenario.Create("Tiny64", 64, 16, 32),
            Scenario.Create("Typical1K", 1_024, 64, 512),
            Scenario.Create("Typical16K", 16 * 1024, 128, 8 * 1024),
            Scenario.Create("Underestimated16K", 16 * 1024, 1_024, 64),
            Scenario.Create("Large100K", 100 * 1024, 4 * 1024, 512),
            Scenario.Create("Large1M", 1024 * 1024, 4 * 1024, 512),
            Scenario.Create("Fragmented100K", 100 * 1024, 1, 512)
        ];

        private int _learnedCapacity = 256;

        [ParamsSource(nameof(ScenarioValues))]
        public Scenario RenderScenario { get; set; }

        public IEnumerable<Scenario> ScenarioValues => Scenarios;

        [Benchmark(Baseline = true)]
        [BenchmarkCategory("StringBuffer")]
        public string ParentContiguous()
        {
            var output = CreateParentBuffer(16 * 1024);
            try
            {
                WriteScenario((IFluidOutput)output);
                return output.ToString();
            }
            finally
            {
                ((IDisposable)output).Dispose();
            }
        }

        [Benchmark]
        [BenchmarkCategory("StringBuffer")]
        public string SegmentedFixed256() => RenderSegmented(256);

        [Benchmark]
        [BenchmarkCategory("StringBuffer")]
        public string SegmentedStaticEstimate() =>
            RenderSegmented(RenderScenario.ProductionStaticEstimate);

        [Benchmark]
        [BenchmarkCategory("StringBuffer")]
        public string SegmentedLearnedCapacity()
        {
            var result = RenderSegmented(_learnedCapacity);
            _learnedCapacity = Math.Min(
                MaximumSegmentCapacity,
                Math.Max(256, result.Length));
            return result;
        }

        public static void PrintMeasurements()
        {
            Console.WriteLine("| Scenario | Strategy | Chars | Growth-copy chars | Char rentals | Max rental | LOH-sized rental |");
            Console.WriteLine("| --- | --- | ---: | ---: | ---: | ---: | --- |");

            foreach (var scenario in Scenarios)
            {
                using (var parent = new ParentBuffer(16 * 1024))
                {
                    WriteScenario(parent, scenario);
                    _ = parent.ToString();
                    Console.WriteLine(
                        $"| {scenario} | Parent contiguous | {scenario.OutputLength} | " +
                        $"{parent.GrowthCopyCount} | {parent.RentCount} | {parent.MaximumRental} | " +
                        $"{(parent.MaximumRental * sizeof(char) >= 85_000 ? "yes" : "no")} |");
                }

                PrintSegmentedMetrics(scenario, 256, "Segmented fixed 256");
                PrintSegmentedMetrics(
                    scenario,
                    scenario.ProductionStaticEstimate,
                    "Segmented static estimate");
            }
        }

        private static void PrintSegmentedMetrics(Scenario scenario, int initialCapacity, string strategy)
        {
            var metrics = MeasureSegmented(scenario, initialCapacity);
            Console.WriteLine(
                $"| {scenario} | {strategy} | {scenario.OutputLength} | {metrics.GrowthCopies} | " +
                $"{metrics.RentCount} | {metrics.MaximumRental} | " +
                $"{(metrics.MaximumRental * sizeof(char) >= 85_000 ? "yes" : "no")} |");
        }

        private string RenderSegmented(int initialCapacity)
        {
            var output = CreateSegmentedBuffer(initialCapacity);
            try
            {
                WriteScenario((IFluidOutput)output);
                return output.ToString();
            }
            finally
            {
                ((IDisposable)output).Dispose();
            }
        }

        private void WriteScenario(IFluidOutput output) => WriteScenario(output, RenderScenario);

        private static void WriteScenario(IFluidOutput output, Scenario scenario)
        {
            foreach (var fragment in scenario.Fragments)
            {
                output.Write(fragment);
            }
        }

        private static Func<int, object> CreateBufferFactory()
        {
            var type = typeof(FluidParser).Assembly.GetType(
                "Fluid.Utils.BufferFluidOutput",
                throwOnError: true);
            var constructor = type.GetConstructor(
                BindingFlags.Instance | BindingFlags.Public,
                binder: null,
                [typeof(int)],
                modifiers: null);
            var capacity = Expression.Parameter(typeof(int), "capacity");
            var create = Expression.New(constructor, capacity);
            return Expression.Lambda<Func<int, object>>(
                Expression.Convert(create, typeof(object)),
                capacity).Compile();
        }

        private static BufferMetrics MeasureSegmented(Scenario scenario, int initialCapacity)
        {
            var capacity = PooledCapacity(initialCapacity);
            var index = 0;
            var hasSegments = false;
            var rents = 1;
            var maximumRental = capacity;
            long growthCopies = 0;

            foreach (var fragment in scenario.Fragments)
            {
                var remainingValue = fragment.Length;
                if (index == 0 && !hasSegments && remainingValue > capacity)
                {
                    var newCapacity = Math.Min(remainingValue, MaximumSegmentCapacity);
                    if (newCapacity > capacity)
                    {
                        capacity = PooledCapacity(newCapacity);
                        rents++;
                        maximumRental = Math.Max(maximumRental, capacity);
                    }
                }
                else if (!hasSegments &&
                    remainingValue > capacity - index &&
                    index <= MaximumSegmentCapacity - remainingValue)
                {
                    var newCapacity = NextCapacity(capacity, index + remainingValue);
                    growthCopies += index;
                    capacity = PooledCapacity(newCapacity);
                    rents++;
                    maximumRental = Math.Max(maximumRental, capacity);
                }

                while (remainingValue > 0)
                {
                    var remainingCapacity = capacity - index;
                    if (remainingCapacity == 0)
                    {
                        hasSegments = true;
                        capacity = PooledCapacity(NextCapacity(
                            capacity,
                            Math.Min(remainingValue, MaximumSegmentCapacity)));
                        rents++;
                        maximumRental = Math.Max(maximumRental, capacity);
                        index = 0;
                        remainingCapacity = capacity;
                    }

                    var count = Math.Min(remainingValue, remainingCapacity);
                    index += count;
                    remainingValue -= count;
                }
            }

            return new BufferMetrics(growthCopies, rents, maximumRental);
        }

        private static int NextCapacity(int currentCapacity, int sizeHint)
        {
            if (sizeHint > MaximumSegmentCapacity)
            {
                return sizeHint;
            }

            var growth = currentCapacity <= MaximumSegmentCapacity / 4
                ? currentCapacity * 4
                : MaximumSegmentCapacity;
            return Math.Max(sizeHint, growth);
        }

        private static int PooledCapacity(int requested)
        {
            var capacity = 16;
            while (capacity < requested)
            {
                capacity *= 2;
            }

            return capacity;
        }

        public sealed class Scenario
        {
            private Scenario(
                string name,
                int outputLength,
                int rootLiteralLength,
                string[] fragments)
            {
                Name = name;
                OutputLength = outputLength;
                var headroom = Math.Min(rootLiteralLength, 4 * 1024);
                ProductionStaticEstimate = Math.Max(
                    256,
                    Math.Min(MaximumSegmentCapacity, rootLiteralLength + headroom));
                Fragments = fragments;
            }

            public string Name { get; }

            public int OutputLength { get; }

            public int ProductionStaticEstimate { get; }

            public string[] Fragments { get; }

            public static Scenario Create(
                string name,
                int outputLength,
                int fragmentLength,
                int rootLiteralLength)
            {
                var fragments = new string[(outputLength + fragmentLength - 1) / fragmentLength];
                var remaining = outputLength;
                for (var i = 0; i < fragments.Length; i++)
                {
                    var length = Math.Min(fragmentLength, remaining);
                    fragments[i] = new string((char)('a' + i % 26), length);
                    remaining -= length;
                }

                return new Scenario(name, outputLength, rootLiteralLength, fragments);
            }

            public override string ToString() => Name;
        }

        private readonly record struct BufferMetrics(
            long GrowthCopies,
            int RentCount,
            int MaximumRental);

        private sealed class ParentBuffer : IFluidOutput, IDisposable
        {
            private char[] _buffer;
            private int _index;

            public ParentBuffer(int initialCapacity)
            {
                _buffer = ArrayPool<char>.Shared.Rent(initialCapacity);
                RentCount = 1;
                MaximumRental = initialCapacity;
            }

            public long GrowthCopyCount { get; private set; }

            public int RentCount { get; private set; }

            public int MaximumRental { get; private set; }

            public void Advance(int count) => _index += count;

            public Memory<char> GetMemory(int sizeHint = 0)
            {
                EnsureCapacity(sizeHint);
                return _buffer.AsMemory(_index);
            }

            public Span<char> GetSpan(int sizeHint = 0)
            {
                EnsureCapacity(sizeHint);
                return _buffer.AsSpan(_index);
            }

            public void Write(string value)
            {
                EnsureCapacity(value.Length);
                value.AsSpan().CopyTo(_buffer.AsSpan(_index));
                _index += value.Length;
            }

            public void Write(char[] buffer, int index, int count)
            {
                EnsureCapacity(count);
                buffer.AsSpan(index, count).CopyTo(_buffer.AsSpan(_index));
                _index += count;
            }

            public ValueTask FlushAsync() => default;

            public override string ToString() =>
                _index == 0 ? string.Empty : new string(_buffer, 0, _index);

            public void Dispose()
            {
                ArrayPool<char>.Shared.Return(_buffer);
                _buffer = null;
            }

            private void EnsureCapacity(int additionalCapacity)
            {
                if (additionalCapacity == 0)
                {
                    additionalCapacity = 1;
                }

                if (_buffer.Length - _index >= additionalCapacity)
                {
                    return;
                }

                var newCapacity = Math.Max(_buffer.Length * 2, _index + additionalCapacity);
                var newBuffer = ArrayPool<char>.Shared.Rent(newCapacity);
                _buffer.AsSpan(0, _index).CopyTo(newBuffer);
                GrowthCopyCount += _index;
                RentCount++;
                MaximumRental = Math.Max(MaximumRental, newCapacity);
                ArrayPool<char>.Shared.Return(_buffer);
                _buffer = newBuffer;
            }
        }
    }
}
