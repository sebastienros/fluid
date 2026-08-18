using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Text.Encodings.Web;
using System.Threading;
using System.Threading.Tasks;
using BenchmarkDotNet.Attributes;

namespace Fluid.Benchmarks
{
    [MemoryDiagnoser]
    public class StaticRenderResolutionBenchmarks
    {
        private const int ItemCount = 100;

        private readonly ReusableFluidOutput _output = new();
        private readonly IFluidTemplate _staticRender;
        private readonly IFluidTemplate _renderWith;
        private readonly IFluidTemplate _renderFor;
        private readonly TemplateContext _staticContext;
        private readonly TemplateContext _withContext;
        private readonly TemplateContext _forContext;
        private readonly TemplateContext _firstContext;
        private readonly TemplateContext _secondContext;
        private readonly VersionedProvider _invalidatedProvider;
        private readonly TemplateContext _invalidatedContext;

        public StaticRenderResolutionBenchmarks()
        {
            var parser = new FluidParser();
            _staticRender = parser.Parse("{% render 'item' %}");
            _renderWith = parser.Parse("{% render 'item.liquid' with value %}");
            _renderFor = parser.Parse("{% render 'item' for items %}");

            _staticContext = CreateContext(new VersionedProvider().Set("item.liquid", "x"));
            _withContext = CreateContext(new UnversionedProvider().Set("item.liquid", "{{ item }}"))
                .SetValue("value", "x");

            var items = new string[ItemCount];
            Array.Fill(items, "x");
            _forContext = CreateContext(new VersionedProvider().Set("item.liquid", "{{ item }}"))
                .SetValue("items", items);

            _firstContext = CreateContext(new VersionedProvider().Set("item.liquid", "x"));
            _secondContext = CreateContext(new VersionedProvider().Set("item.liquid", "x"));

            _invalidatedProvider = new VersionedProvider().Set("item.liquid", "x");
            _invalidatedContext = CreateContext(_invalidatedProvider);

            Warm(_staticRender, _staticContext);
            Warm(_renderWith, _withContext);
            Warm(_renderFor, _forContext);
            Warm(_staticRender, _firstContext);
            Warm(_staticRender, _secondContext);
            Warm(_staticRender, _invalidatedContext);
        }

        [Benchmark]
        public ValueTask WarmStaticRender() => Render(_staticRender, _staticContext);

        [Benchmark]
        public ValueTask WarmRenderWithIdentifier() => Render(_renderWith, _withContext);

        [Benchmark]
        public ValueTask WarmRenderFor() => Render(_renderFor, _forContext);

        [Benchmark]
        public async ValueTask MultipleOptions()
        {
            await Render(_staticRender, _firstContext);
            await Render(_staticRender, _secondContext);
        }

        [Benchmark]
        public ValueTask InvalidatedStaticRender()
        {
            _invalidatedProvider.Set("item.liquid", "x");
            return Render(_staticRender, _invalidatedContext);
        }

        private ValueTask Render(IFluidTemplate template, TemplateContext context)
        {
            _output.Reset();
            return template.RenderAsync(_output, NullEncoder.Default, context);
        }

        private void Warm(IFluidTemplate template, TemplateContext context) =>
            Render(template, context).GetAwaiter().GetResult();

        private static TemplateContext CreateContext(ITemplateFileProvider provider) =>
            new(new TemplateOptions { FileProvider = provider });

        private class UnversionedProvider : ITemplateFileProvider
        {
            private readonly Dictionary<string, Entry> _files = new(StringComparer.Ordinal);
            private long _lastModified;

            public int Calls { get; private set; }

            public UnversionedProvider Set(string path, string content)
            {
                _files[path] = new Entry(
                    Encoding.UTF8.GetBytes(content),
                    DateTimeOffset.UnixEpoch.AddTicks(++_lastModified));
                return this;
            }

            public ValueTask<TemplateSourceInfo> GetFileInfoAsync(
                string subpath,
                TemplateContext context,
                CancellationToken cancellationToken)
            {
                Calls++;
                cancellationToken.ThrowIfCancellationRequested();

                if (!_files.TryGetValue(subpath, out var entry))
                {
                    return default;
                }

                return new ValueTask<TemplateSourceInfo>(
                    new TemplateSourceInfo(
                        entry.LastModified,
                        _ => new ValueTask<Stream>(new MemoryStream(entry.Content, writable: false))));
            }

            private sealed record Entry(byte[] Content, DateTimeOffset LastModified);
        }

        private sealed class VersionedProvider : UnversionedProvider, IVersionedTemplateFileProvider
        {
            private long _version;

            public long Version => Interlocked.Read(ref _version);

            public object GetTemplateResolutionCacheKey(TemplateContext context) => this;

            public new VersionedProvider Set(string path, string content)
            {
                base.Set(path, content);
                Interlocked.Increment(ref _version);
                return this;
            }
        }

        private sealed class ReusableFluidOutput : IFluidOutput
        {
            private char[] _buffer = new char[1024];

            public int Written { get; private set; }

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

            public ValueTask FlushAsync() => default;

            public void Reset() => Written = 0;

            private void EnsureCapacity(int sizeHint)
            {
                var required = Written + Math.Max(sizeHint, 1);
                if (required > _buffer.Length)
                {
                    Array.Resize(ref _buffer, Math.Max(required, _buffer.Length * 2));
                }
            }
        }
    }
}
