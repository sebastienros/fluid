using System;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using BenchmarkDotNet.Attributes;

namespace Fluid.Benchmarks
{
    [MemoryDiagnoser]
    public class NestedRenderBenchmarks
    {
        private const int ItemCount = 100;

        private readonly CountingFluidOutput _output = new();
        private readonly TemplateContext _context;
        private readonly IFluidTemplate _renderTemplate;
        private readonly IFluidTemplate _renderForTemplate;

        public NestedRenderBenchmarks()
        {
            var parser = new FluidParser();
            var options = new TemplateOptions
            {
                FileProvider = new InMemoryTemplateFileProvider("product.liquid", "{{ product }};")
            };

            _context = new TemplateContext(options);
            _context.SetValue("products", Enumerable.Range(1, ItemCount).ToArray());

            _renderTemplate = parser.Parse("{% render 'product', product: products[0] %}");
            _renderForTemplate = parser.Parse("{% render 'product' for products as product %}");
        }

        [Benchmark]
        public async ValueTask<int> Render()
        {
            _output.Reset();
            await _renderTemplate.RenderAsync(_output, NullEncoder.Default, _context);
            return _output.Written;
        }

        [Benchmark]
        public async ValueTask<int> RenderFor()
        {
            _output.Reset();
            await _renderForTemplate.RenderAsync(_output, NullEncoder.Default, _context);
            return _output.Written;
        }

        private sealed class CountingFluidOutput : IFluidOutput
        {
            private char[] _buffer = new char[1024];

            public int Written { get; private set; }

            public int FlushCount { get; private set; }

            public void Advance(int count) => Written += count;

            public Memory<char> GetMemory(int sizeHint = 0)
            {
                EnsureCapacity(sizeHint);
                return _buffer.AsMemory(Written);
            }

            public Span<char> GetSpan(int sizeHint = 0)
            {
                EnsureCapacity(sizeHint);
                return _buffer.AsSpan(Written);
            }

            public void Write(string value)
            {
                EnsureCapacity(value.Length);
                value.CopyTo(0, _buffer, Written, value.Length);
                Written += value.Length;
            }

            public void Write(char[] buffer, int index, int count)
            {
                EnsureCapacity(count);
                buffer.AsSpan(index, count).CopyTo(_buffer.AsSpan(Written));
                Written += count;
            }

            public ValueTask FlushAsync()
            {
                FlushCount++;
                return default;
            }

            public void Reset()
            {
                Written = 0;
                FlushCount = 0;
            }

            private void EnsureCapacity(int sizeHint)
            {
                var required = Written + Math.Max(sizeHint, 1);
                if (required > _buffer.Length)
                {
                    Array.Resize(ref _buffer, Math.Max(required, _buffer.Length * 2));
                }
            }
        }

        private sealed class InMemoryTemplateFileProvider : ITemplateFileProvider
        {
            private readonly byte[] _content;
            private readonly string _path;

            public InMemoryTemplateFileProvider(string path, string content)
            {
                _path = path;
                _content = Encoding.UTF8.GetBytes(content);
            }

            public ValueTask<TemplateSourceInfo> GetFileInfoAsync(
                string subpath,
                TemplateContext context,
                CancellationToken cancellationToken)
            {
                cancellationToken.ThrowIfCancellationRequested();

                if (!string.Equals(subpath, _path, StringComparison.Ordinal))
                {
                    return default;
                }

                return new ValueTask<TemplateSourceInfo>(
                    new TemplateSourceInfo(
                        DateTimeOffset.UnixEpoch,
                        _ => new ValueTask<Stream>(new MemoryStream(_content, writable: false))));
            }
        }
    }
}
