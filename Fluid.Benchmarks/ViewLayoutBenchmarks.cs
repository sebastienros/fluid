using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using BenchmarkDotNet.Attributes;
using Fluid.ViewEngine;

namespace Fluid.Benchmarks;

/// <summary>
/// Renders views through <see cref="FluidViewRenderer"/> with no layout, one layout, and nested layouts.
/// </summary>
[MemoryDiagnoser]
public class ViewLayoutBenchmarks
{
    private static readonly string Paragraphs = string.Concat(
        System.Linq.Enumerable.Repeat("<p>Lorem ipsum dolor sit amet, consectetur adipiscing elit, sed do eiusmod tempor.</p>\n", 30));

    private readonly ReusableFluidOutput _output = new();
    private readonly FluidViewRenderer _renderer;
    private readonly TemplateOptions _templateOptions;

    public ViewLayoutBenchmarks()
    {
        var provider = new InMemoryTemplateFileProvider(
            ("NoLayout.liquid", "<h1>{{ title }}</h1>" + Paragraphs),
            ("OneLayout.liquid", "{% layout '_Layout' %}<h1>{{ title }}</h1>{% section scripts %}<script></script>{% endsection %}" + Paragraphs),
            ("NestedLayouts.liquid", "{% layout '_Inner' %}<h1>{{ title }}</h1>{% section scripts %}<script></script>{% endsection %}" + Paragraphs),
            ("_Layout.liquid", "<html><head><title>{{ title }}</title></head><body>" + Paragraphs + "{% renderbody %}{% rendersection scripts %}</body></html>"),
            ("_Inner.liquid", "{% layout '_Layout' %}<main>" + Paragraphs + "{% renderbody %}</main>"));

        var options = new FluidViewEngineOptions { ViewsFileProvider = provider };
        options.LayoutsLocationFormats.Add("{0}" + Constants.ViewExtension);
        options.TrackFileChanges = false;

        _templateOptions = options.TemplateOptions;
        _renderer = new FluidViewRenderer(options);

        // Parse and cache every template.
        NoLayout().GetAwaiter().GetResult();
        OneLayout().GetAwaiter().GetResult();
        NestedLayouts().GetAwaiter().GetResult();
    }

    [Benchmark(Baseline = true)]
    public Task NoLayout() => RenderAsync("NoLayout.liquid");

    [Benchmark]
    public Task OneLayout() => RenderAsync("OneLayout.liquid");

    [Benchmark]
    public Task NestedLayouts() => RenderAsync("NestedLayouts.liquid");

    private Task RenderAsync(string path)
    {
        _output.Reset();
        var context = new TemplateContext(_templateOptions);
        context.SetValue("title", "Fluid");
        return _renderer.RenderViewAsync(_output, path, context);
    }

    private sealed class ReusableFluidOutput : IFluidOutput
    {
        private char[] _buffer = new char[16 * 1024];
        private int _written;

        public void Reset() => _written = 0;

        public void Advance(int count) => _written += count;

        public Memory<char> GetMemory(int sizeHint = 0)
        {
            EnsureCapacity(sizeHint);
            return _buffer.AsMemory(_written);
        }

        public Span<char> GetSpan(int sizeHint = 0)
        {
            EnsureCapacity(sizeHint);
            return _buffer.AsSpan(_written);
        }

        public void Write(string value)
        {
            EnsureCapacity(value.Length);
            value.CopyTo(0, _buffer, _written, value.Length);
            _written += value.Length;
        }

        public void Write(char[] buffer, int index, int count)
        {
            EnsureCapacity(count);
            buffer.AsSpan(index, count).CopyTo(_buffer.AsSpan(_written));
            _written += count;
        }

        public void Write(char value)
        {
            EnsureCapacity(1);
            _buffer[_written++] = value;
        }

        public ValueTask FlushAsync() => default;

        private void EnsureCapacity(int sizeHint)
        {
            sizeHint = Math.Max(sizeHint, 1);
            if (_buffer.Length - _written < sizeHint)
            {
                Array.Resize(ref _buffer, Math.Max(_buffer.Length * 2, _written + sizeHint));
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
            if (!_templates.TryGetValue(subpath.TrimStart('/'), out var content))
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
