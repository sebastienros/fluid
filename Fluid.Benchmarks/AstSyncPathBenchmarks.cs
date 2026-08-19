using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Text.Encodings.Web;
using System.Threading;
using System.Threading.Tasks;
using BenchmarkDotNet.Attributes;
using Fluid.Ast;
using Fluid.Values;

namespace Fluid.Benchmarks
{
    [MemoryDiagnoser]
    public class AstSyncPathBenchmarks
    {
        private const int ItemCount = 100;

        private readonly CountingFluidOutput _output = new();
        private readonly TemplateContext _forContext;
        private readonly TemplateContext _renderContext;
        private readonly TemplateContext _includeContext;
        private readonly ForStatement _for;
        private readonly ForStatement _forAsync;
        private readonly RenderStatement _render;
        private readonly RenderStatement _renderAsync;
        private readonly IncludeStatement _include;
        private readonly IncludeStatement _includeAsync;

        public AstSyncPathBenchmarks()
        {
            var values = new FluidValue[ItemCount];
            for (var i = 0; i < values.Length; i++)
            {
                values[i] = NumberValue.Create(i);
            }

            var source = new LiteralExpression(new ArrayValue(values));
            _forContext = new TemplateContext();
            _for = new ForStatement(
                [new TextSpanStatement("x")],
                "item",
                source,
                limit: null,
                offset: null,
                reversed: false);
            _forAsync = new ForStatement(
                [new SuspendingStatement()],
                "item",
                source,
                limit: null,
                offset: null,
                reversed: false);

            var parser = new FluidParser();
            _renderContext = CreateTemplateContext(parser, out var renderPath);
            _includeContext = CreateTemplateContext(parser, out var includePath);

            _render = new RenderStatement(parser, renderPath);
            _renderAsync = new RenderStatement(parser, renderPath + "-async");
            _include = new IncludeStatement(parser, new LiteralExpression(new StringValue(includePath)));
            _includeAsync = new IncludeStatement(parser, new LiteralExpression(new StringValue(includePath + "-async")));

            WarmTemplateCaches();
        }

        [Benchmark]
        public ValueTask<Completion> For() => Run(_for, _forContext);

        [Benchmark]
        public ValueTask<Completion> ForAsync() => Run(_forAsync, _forContext);

        [Benchmark]
        public ValueTask<Completion> Render() => Run(_render, _renderContext);

        [Benchmark]
        public ValueTask<Completion> RenderAsync() => Run(_renderAsync, _renderContext);

        [Benchmark]
        public ValueTask<Completion> Include() => Run(_include, _includeContext);

        [Benchmark]
        public ValueTask<Completion> IncludeAsync() => Run(_includeAsync, _includeContext);

        private ValueTask<Completion> Run(Statement statement, TemplateContext context)
        {
            _output.Reset();
            return statement.WriteToAsync(_output, NullEncoder.Default, context);
        }

        private static TemplateContext CreateTemplateContext(FluidParser parser, out string path)
        {
            path = Guid.NewGuid().ToString("N");
            var provider = new BenchmarkTemplateFileProvider()
                .Add(path, "x")
                .Add(path + "-async", "");
            var options = new TemplateOptions
            {
                FileProvider = provider,
                TemplateParsed = (templatePath, template) =>
                    templatePath.EndsWith("-async", StringComparison.Ordinal)
                        ? new SuspendingTemplate()
                        : template
            };

            return new TemplateContext(options);
        }

        private void WarmTemplateCaches()
        {
            Run(_render, _renderContext).GetAwaiter().GetResult();
            Run(_renderAsync, _renderContext).GetAwaiter().GetResult();
            Run(_include, _includeContext).GetAwaiter().GetResult();
            Run(_includeAsync, _includeContext).GetAwaiter().GetResult();
        }

        private sealed class SuspendingStatement : Statement
        {
            public override async ValueTask<Completion> WriteToAsync(
                IFluidOutput output,
                TextEncoder encoder,
                TemplateContext context)
            {
                await Task.Yield();
                output.Write("x");
                return Completion.Normal;
            }

            protected override Statement Accept(AstVisitor visitor) => this;
        }

        private sealed class SuspendingTemplate : IFluidTemplate
        {
            public async ValueTask RenderAsync(
                IFluidOutput output,
                TextEncoder encoder,
                TemplateContext context)
            {
                await Task.Yield();
                output.Write("x");
            }
        }

        private sealed class BenchmarkTemplateFileProvider : ITemplateFileProvider
        {
            private readonly Dictionary<string, byte[]> _files = new(StringComparer.Ordinal);

            public BenchmarkTemplateFileProvider Add(string path, string content)
            {
                _files[path] = Encoding.UTF8.GetBytes(content);
                return this;
            }

            public ValueTask<TemplateSourceInfo> GetFileInfoAsync(
                string subpath,
                TemplateContext context,
                CancellationToken cancellationToken)
            {
                cancellationToken.ThrowIfCancellationRequested();
                if (!_files.TryGetValue(subpath, out var content))
                {
                    return default;
                }

                return new ValueTask<TemplateSourceInfo>(
                    new TemplateSourceInfo(
                        DateTimeOffset.UnixEpoch,
                        _ => new ValueTask<Stream>(new MemoryStream(content, writable: false))));
            }
        }

        private sealed class CountingFluidOutput : IFluidOutput
        {
            private char[] _buffer = new char[128];

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
