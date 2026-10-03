using BenchmarkDotNet.Attributes;
using BenchmarkDotNet.Configs;
using Fluid.Utils;
using System;
using System.Buffers;
using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Text.Encodings.Web;
using System.Threading;
using System.Threading.Tasks;

namespace Fluid.Benchmarks
{
    [MemoryDiagnoser]
    [GroupBenchmarksBy(BenchmarkLogicalGroupRule.ByCategory)]
    [ShortRunJob]
    public class Utf8OutputBenchmarks
    {
        private static readonly UTF8Encoding Utf8 = new UTF8Encoding(false);
        private static readonly Scenario[] Scenarios =
        [
            new(
                "SmallAscii",
                "Hello {{ name }}!",
                () => new TemplateContext().SetValue("name", "Fluid"),
                NullEncoder.Default),
            new(
                "ProductTemplate",
                """
                <section class="products">
                {% for product in products %}
                  <article data-id="{{ product.id }}">
                    <h2>{{ product.name }}</h2>
                    <p>{{ product.description }}</p>
                    <strong>{{ product.price }}</strong>
                  </article>
                {% endfor %}
                </section>
                """,
                CreateProductContext,
                HtmlEncoder.Default),
            new(
                "UnicodeEncoded",
                "<h1>{{ title }}</h1><p>{{ description }}</p>",
                () => new TemplateContext()
                    .SetValue("title", "\u65b0\u5546\u54c1 \ud83d\ude80")
                    .SetValue("description", "<strong>Cr\u00e8me & caf\u00e9</strong>"),
                HtmlEncoder.Default),
            new(
                "LargeOutput",
                "{{ content }}",
                () => new TemplateContext().SetValue("content", new string('x', 256 * 1024)),
                NullEncoder.Default)
        ];

        private readonly CountingStream _stream = new();
        private readonly CountingByteWriter _byteWriter = new(512 * 1024);
        private IFluidTemplate _template;

        [ParamsSource(nameof(ScenarioValues))]
        public Scenario BenchmarkScenario { get; set; }

        public IEnumerable<Scenario> ScenarioValues => Scenarios;

        [GlobalSetup]
        public async Task Setup()
        {
            _template = new FluidParser().Parse(BenchmarkScenario.Template);

            var baseline = await TextWriterThenUtf8();
            var candidate = await DirectUtf8();
            if (baseline.BytesWritten != candidate.BytesWritten ||
                baseline.FlushCount == 0 ||
                candidate.FlushCount == 0)
            {
                throw new InvalidOperationException(
                    $"Invalid output metrics for {BenchmarkScenario}: {baseline} vs {candidate}.");
            }
        }

        [Benchmark(Baseline = true)]
        [BenchmarkCategory("UTF8")]
        public async ValueTask<RenderMeasurement> TextWriterThenUtf8()
        {
            _stream.Reset();

            await using (var writer = new StreamWriter(
                _stream,
                Utf8,
                bufferSize: 1024,
                leaveOpen: true))
            {
                await using (var output = new TextWriterFluidOutput(
                    writer,
                    bufferSize: 16 * 1024,
                    leaveOpen: true,
                    allowSynchronousIO: false))
                {
                    await _template.RenderAsync(
                        output,
                        BenchmarkScenario.Encoder,
                        BenchmarkScenario.CreateContext());
                }

            }

            return new RenderMeasurement(_stream.BytesWritten, _stream.FlushCount);
        }

        [Benchmark]
        [BenchmarkCategory("UTF8")]
        public async ValueTask<RenderMeasurement> DirectUtf8()
        {
            _byteWriter.Reset();

            await _template.RenderAsync(
                _byteWriter,
                BenchmarkScenario.Encoder,
                BenchmarkScenario.CreateContext());

            await _byteWriter.FlushAsync();
            return new RenderMeasurement(_byteWriter.BytesWritten, _byteWriter.FlushCount);
        }

        public static async Task PrintMeasurementsAsync()
        {
            Console.WriteLine("| Scenario | Path | Bytes written | Flushes |");
            Console.WriteLine("| --- | --- | ---: | ---: |");

            foreach (var scenario in Scenarios)
            {
                var benchmark = new Utf8OutputBenchmarks { BenchmarkScenario = scenario };
                await benchmark.Setup();

                var baseline = await benchmark.TextWriterThenUtf8();
                var candidate = await benchmark.DirectUtf8();
                Console.WriteLine($"| {scenario} | TextWriter + UTF-8 | {baseline.BytesWritten} | {baseline.FlushCount} |");
                Console.WriteLine($"| {scenario} | Direct UTF-8 | {candidate.BytesWritten} | {candidate.FlushCount} |");
            }
        }

        private static TemplateContext CreateProductContext()
        {
            var products = new List<Dictionary<string, object>>(100);
            for (var i = 0; i < 100; i++)
            {
                products.Add(new Dictionary<string, object>
                {
                    ["id"] = i,
                    ["name"] = "Product " + i,
                    ["description"] = "A practical <item> for home & office.",
                    ["price"] = 19.95m + i
                });
            }

            return new TemplateContext().SetValue("products", products);
        }

        public sealed class Scenario
        {
            public Scenario(
                string name,
                string template,
                Func<TemplateContext> createContext,
                TextEncoder encoder)
            {
                Name = name;
                Template = template;
                CreateContext = createContext;
                Encoder = encoder;
            }

            public string Name { get; }

            public string Template { get; }

            public Func<TemplateContext> CreateContext { get; }

            public TextEncoder Encoder { get; }

            public override string ToString() => Name;
        }

        public readonly record struct RenderMeasurement(long BytesWritten, int FlushCount);

        private sealed class CountingByteWriter : IBufferWriter<byte>
        {
            private readonly byte[] _buffer;
            private int _index;

            public CountingByteWriter(int capacity)
            {
                _buffer = new byte[capacity];
            }

            public long BytesWritten => _index;

            public int FlushCount { get; private set; }

            public void Advance(int count) => _index += count;

            public Memory<byte> GetMemory(int sizeHint = 0) => _buffer.AsMemory(_index);

            public Span<byte> GetSpan(int sizeHint = 0) => _buffer.AsSpan(_index);

            public ValueTask FlushAsync()
            {
                FlushCount++;
                return default;
            }

            public void Reset()
            {
                _index = 0;
                FlushCount = 0;
            }
        }

        private sealed class CountingStream : Stream
        {
            public long BytesWritten { get; private set; }

            public int FlushCount { get; private set; }

            public override bool CanRead => false;

            public override bool CanSeek => false;

            public override bool CanWrite => true;

            public override long Length => BytesWritten;

            public override long Position
            {
                get => BytesWritten;
                set => throw new NotSupportedException();
            }

            public override void Flush() => FlushCount++;

            public override Task FlushAsync(CancellationToken cancellationToken)
            {
                cancellationToken.ThrowIfCancellationRequested();
                FlushCount++;
                return Task.CompletedTask;
            }

            public override void Write(byte[] buffer, int offset, int count) => BytesWritten += count;

            public override Task WriteAsync(
                byte[] buffer,
                int offset,
                int count,
                CancellationToken cancellationToken)
            {
                cancellationToken.ThrowIfCancellationRequested();
                BytesWritten += count;
                return Task.CompletedTask;
            }

            public override ValueTask WriteAsync(
                ReadOnlyMemory<byte> buffer,
                CancellationToken cancellationToken = default)
            {
                cancellationToken.ThrowIfCancellationRequested();
                BytesWritten += buffer.Length;
                return default;
            }

            public void Reset()
            {
                BytesWritten = 0;
                FlushCount = 0;
            }

            public override int Read(byte[] buffer, int offset, int count) =>
                throw new NotSupportedException();

            public override long Seek(long offset, SeekOrigin origin) =>
                throw new NotSupportedException();

            public override void SetLength(long value) =>
                throw new NotSupportedException();
        }
    }
}
