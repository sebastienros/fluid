using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using BenchmarkDotNet.Attributes;

namespace Fluid.Benchmarks;

/// <summary>
/// A partial rendered from inside a loop, the shape of a product grid or a comment list. The partial
/// is referenced without its extension, as Liquid templates usually do, so resolving it probes the
/// file provider for the bare name before the name with the default extension.
/// </summary>
[MemoryDiagnoser]
public class PartialLoopBenchmarks
{
    private const int Iterations = 100;
    private const string Partial = "<li>{{ i }}</li>";

    private readonly string _directory;
    private readonly TemplateOptions _fileSystemOptions;
    private readonly TemplateOptions _inMemoryOptions;
    private readonly IFluidTemplate _renderTemplate;
    private readonly IFluidTemplate _includeTemplate;

    public PartialLoopBenchmarks()
    {
        _directory = Path.Combine(Path.GetTempPath(), "FluidBenchmarks", Path.GetRandomFileName());
        Directory.CreateDirectory(_directory);
        File.WriteAllText(Path.Combine(_directory, "item.liquid"), Partial);

        _fileSystemOptions = new TemplateOptionsBuilder()
            .WithFileProvider(new FileSystemTemplateFileProvider(_directory))
            .Build();

        _inMemoryOptions = new TemplateOptionsBuilder()
            .WithFileProvider(new InMemoryTemplateFileProvider(("item.liquid", Partial)))
            .Build();

        var parser = new FluidParser();
        _renderTemplate = parser.Parse($"{{% for i in (1..{Iterations}) %}}{{% render 'item', i: i %}}{{% endfor %}}");
        _includeTemplate = parser.Parse($"{{% for i in (1..{Iterations}) %}}{{% include 'item' %}}{{% endfor %}}");

        CheckAll();
    }

    [GlobalCleanup]
    public void Cleanup()
    {
        Directory.Delete(_directory, recursive: true);
    }

    private void CheckAll()
    {
        Verify(nameof(RenderFromFileSystem), RenderFromFileSystem());
        Verify(nameof(IncludeFromFileSystem), IncludeFromFileSystem());
        Verify(nameof(RenderFromMemory), RenderFromMemory());
        Verify(nameof(IncludeFromMemory), IncludeFromMemory());

        static void Verify(string name, string result)
        {
            if (string.IsNullOrEmpty(result) || !result.EndsWith($"<li>{Iterations}</li>", StringComparison.Ordinal))
            {
                throw new InvalidOperationException($"{name} rendering failed: {result}");
            }
        }
    }

    [Benchmark]
    public string RenderFromFileSystem()
    {
        return _renderTemplate.Render(new TemplateContext(_fileSystemOptions));
    }

    [Benchmark]
    public string IncludeFromFileSystem()
    {
        return _includeTemplate.Render(new TemplateContext(_fileSystemOptions));
    }

    [Benchmark]
    public string RenderFromMemory()
    {
        return _renderTemplate.Render(new TemplateContext(_inMemoryOptions));
    }

    [Benchmark]
    public string IncludeFromMemory()
    {
        return _includeTemplate.Render(new TemplateContext(_inMemoryOptions));
    }

    /// <summary>
    /// Stats the file on every lookup, like a physical file provider does to report the last
    /// modification date.
    /// </summary>
    private sealed class FileSystemTemplateFileProvider : ITemplateFileProvider
    {
        private readonly string _root;

        public FileSystemTemplateFileProvider(string root)
        {
            _root = root;
        }

        public ValueTask<TemplateSourceInfo> GetFileInfoAsync(
            string subpath,
            TemplateContext context,
            CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();

            var fileInfo = new FileInfo(Path.Combine(_root, subpath));
            if (!fileInfo.Exists)
            {
                return default;
            }

            return new ValueTask<TemplateSourceInfo>(
                new TemplateSourceInfo(
                    fileInfo.LastWriteTimeUtc,
                    _ => new ValueTask<Stream>(fileInfo.OpenRead())));
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
