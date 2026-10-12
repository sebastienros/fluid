using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using BenchmarkDotNet.Attributes;

namespace Fluid.Benchmarks;

/// <summary>
/// Partials rendered in a loop to a <see cref="TextWriter"/>, where what is written goes through a
/// buffer before reaching the writer. The inlined variant renders the same output without partials.
/// </summary>
[MemoryDiagnoser]
public class PartialFlushBenchmarks
{
    private const int Iterations = 100;
    private const string Partial = "<li>{{ i }}</li>";

    private readonly TemplateOptions _options;
    private readonly IFluidTemplate _renderTemplate;
    private readonly IFluidTemplate _includeTemplate;
    private readonly IFluidTemplate _inlinedTemplate;
    private readonly MemoryStream _stream = new();
    private readonly StreamWriter _writer;

    public PartialFlushBenchmarks()
    {
        _writer = new StreamWriter(_stream, new UTF8Encoding(encoderShouldEmitUTF8Identifier: false));

        _options = new TemplateOptionsBuilder()
            .WithFileProvider(new InMemoryTemplateFileProvider(("item.liquid", Partial)))
            .Build();

        var parser = new FluidParser();
        _renderTemplate = parser.Parse($"{{% for i in (1..{Iterations}) %}}{{% render 'item', i: i %}}{{% endfor %}}");
        _includeTemplate = parser.Parse($"{{% for i in (1..{Iterations}) %}}{{% include 'item' %}}{{% endfor %}}");
        _inlinedTemplate = parser.Parse($"{{% for i in (1..{Iterations}) %}}{Partial}{{% endfor %}}");

        Verify(nameof(Render), Render());
        Verify(nameof(Include), Include());
        Verify(nameof(Inlined), Inlined());

        void Verify(string name, Task task)
        {
            task.GetAwaiter().GetResult();

            var result = Encoding.UTF8.GetString(_stream.GetBuffer(), 0, (int)_stream.Length);
            if (!result.StartsWith("<li>1</li><li>2</li>", StringComparison.Ordinal) || !result.EndsWith($"<li>{Iterations}</li>", StringComparison.Ordinal))
            {
                throw new InvalidOperationException($"{name} rendering failed: {result}");
            }
        }
    }

    [Benchmark]
    public Task Render()
    {
        return RenderAsync(_renderTemplate);
    }

    [Benchmark]
    public Task Include()
    {
        return RenderAsync(_includeTemplate);
    }

    [Benchmark]
    public Task Inlined()
    {
        return RenderAsync(_inlinedTemplate);
    }

    private async Task RenderAsync(IFluidTemplate template)
    {
        _stream.SetLength(0);
        await template.RenderAsync(_writer, new TemplateContext(_options));
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
