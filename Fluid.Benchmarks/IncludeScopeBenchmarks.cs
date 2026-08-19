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
    public class IncludeScopeBenchmarks
    {
        private readonly CountingFluidOutput _output = new();
        private readonly TemplateContext _context;
        private readonly IFluidTemplate _oneKeywordArgument;
        private readonly IFluidTemplate _multipleKeywordArguments;
        private readonly IFluidTemplate _nestedIncludes;
        private readonly IFluidTemplate _repeatedIncludes;

        public IncludeScopeBenchmarks()
        {
            var parser = new FluidParser();
            var options = new TemplateOptions
            {
                FileProvider = new InMemoryTemplateFileProvider(
                    ("value.liquid", "{{ value }}"),
                    ("arguments.liquid", "{{ first }}{{ second }}{{ third }}"),
                    ("outer.liquid", "{% include 'value', value: value %}"))
            };

            _context = new TemplateContext(options)
                .SetValue("value", "v");

            _oneKeywordArgument = parser.Parse("{% include 'value', value: value %}");
            _multipleKeywordArguments = parser.Parse(
                "{% include 'arguments', first: value, second: value, third: value %}");
            _nestedIncludes = parser.Parse("{% include 'outer', value: value %}");
            _repeatedIncludes = parser.Parse(
                "{% include 'value', value: value %}" +
                "{% include 'value', value: value %}" +
                "{% include 'value', value: value %}" +
                "{% include 'value', value: value %}");

            WarmUp();
        }

        [Benchmark]
        public ValueTask OneKeywordArgument()
        {
            _output.Reset();
            return _oneKeywordArgument.RenderAsync(_output, NullEncoder.Default, _context);
        }

        [Benchmark]
        public ValueTask MultipleKeywordArguments()
        {
            _output.Reset();
            return _multipleKeywordArguments.RenderAsync(_output, NullEncoder.Default, _context);
        }

        [Benchmark]
        public ValueTask NestedIncludes()
        {
            _output.Reset();
            return _nestedIncludes.RenderAsync(_output, NullEncoder.Default, _context);
        }

        [Benchmark]
        public ValueTask RepeatedIncludes()
        {
            _output.Reset();
            return _repeatedIncludes.RenderAsync(_output, NullEncoder.Default, _context);
        }

        private void WarmUp()
        {
            OneKeywordArgument().GetAwaiter().GetResult();
            MultipleKeywordArguments().GetAwaiter().GetResult();
            NestedIncludes().GetAwaiter().GetResult();
            RepeatedIncludes().GetAwaiter().GetResult();
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
