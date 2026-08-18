using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using BenchmarkDotNet.Attributes;

namespace Fluid.Benchmarks
{
    [MemoryDiagnoser]
    public class RenderScopeBenchmarks
    {
        private readonly CountingFluidOutput _output = new();
        private readonly TemplateContext _context;
        private readonly IFluidTemplate _noArguments;
        private readonly IFluidTemplate _withAlias;
        private readonly IFluidTemplate _keywordArguments;
        private readonly IFluidTemplate _renderFor;
        private readonly IFluidTemplate _nestedRender;

        public RenderScopeBenchmarks()
        {
            var parser = new FluidParser();
            var options = new TemplateOptions
            {
                FileProvider = new InMemoryTemplateFileProvider(
                    ("empty.liquid", "x"),
                    ("value.liquid", "{{ value }}"),
                    ("arguments.liquid", "{{ first }}{{ second }}"),
                    ("outer.liquid", "{% render 'value' with value as value %}"))
            };

            _context = new TemplateContext(options)
                .SetValue("value", "v")
                .SetValue("values", new[] { "a", "b", "c" });

            _noArguments = parser.Parse("{% render 'empty' %}");
            _withAlias = parser.Parse("{% render 'value' with value as value %}");
            _keywordArguments = parser.Parse("{% render 'arguments', first: value, second: value %}");
            _renderFor = parser.Parse("{% render 'value' for values as value %}");
            _nestedRender = parser.Parse("{% render 'outer' with value as value %}");

            WarmUp();
        }

        [Benchmark]
        public ValueTask NoArguments()
        {
            _output.Reset();
            return _noArguments.RenderAsync(_output, NullEncoder.Default, _context);
        }

        [Benchmark]
        public ValueTask WithAlias()
        {
            _output.Reset();
            return _withAlias.RenderAsync(_output, NullEncoder.Default, _context);
        }

        [Benchmark]
        public ValueTask KeywordArguments()
        {
            _output.Reset();
            return _keywordArguments.RenderAsync(_output, NullEncoder.Default, _context);
        }

        [Benchmark]
        public ValueTask RenderFor()
        {
            _output.Reset();
            return _renderFor.RenderAsync(_output, NullEncoder.Default, _context);
        }

        [Benchmark]
        public ValueTask NestedRender()
        {
            _output.Reset();
            return _nestedRender.RenderAsync(_output, NullEncoder.Default, _context);
        }

        private void WarmUp()
        {
            NoArguments().GetAwaiter().GetResult();
            WithAlias().GetAwaiter().GetResult();
            KeywordArguments().GetAwaiter().GetResult();
            RenderFor().GetAwaiter().GetResult();
            NestedRender().GetAwaiter().GetResult();
        }

        private sealed class CountingFluidOutput : IFluidOutput
        {
            private char[] _buffer = new char[64];

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

        private sealed class InMemoryTemplateFileProvider : ITemplateFileProvider
        {
            private readonly Dictionary<string, byte[]> _templates = new(StringComparer.Ordinal);

            public InMemoryTemplateFileProvider(params (string Path, string Content)[] templates)
            {
                foreach (var template in templates)
                {
                    _templates[template.Path] = Encoding.UTF8.GetBytes(template.Content);
                }
            }

            public ValueTask<TemplateSourceInfo> GetFileInfoAsync(
                string subpath,
                TemplateContext context,
                CancellationToken cancellationToken)
            {
                cancellationToken.ThrowIfCancellationRequested();

                if (!_templates.TryGetValue(subpath, out var content))
                {
                    return default;
                }

                return new ValueTask<TemplateSourceInfo>(
                    new TemplateSourceInfo(
                        DateTimeOffset.UnixEpoch,
                        _ => new ValueTask<Stream>(new MemoryStream(content, writable: false))));
            }
        }
    }
}
