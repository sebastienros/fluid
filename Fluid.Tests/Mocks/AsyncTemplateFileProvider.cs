using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace Fluid.Tests.Mocks;

public sealed class AsyncTemplateFileProvider : ITemplateFileProvider
{
    private sealed record Entry(string Content, DateTimeOffset LastModified);

    private readonly Dictionary<string, Entry> _sources = new(StringComparer.OrdinalIgnoreCase);
    private readonly Dictionary<string, int> _readCounts = new(StringComparer.OrdinalIgnoreCase);
    private long _version;

    public List<string> RequestedPaths { get; } = [];
    public CancellationToken LastCancellationToken { get; private set; }

    public AsyncTemplateFileProvider Add(string path, string content)
    {
        _sources[NormalizePath(path)] = new Entry(
            content,
            new DateTimeOffset(2020, 1, 1, 0, 0, 0, TimeSpan.Zero).AddTicks(++_version));
        return this;
    }

    public AsyncTemplateFileProvider Remove(string path)
    {
        _sources.Remove(NormalizePath(path));
        return this;
    }

    public int GetReadCount(string path) =>
        _readCounts.TryGetValue(NormalizePath(path), out var count) ? count : 0;

    public async ValueTask<TemplateSourceInfo> GetFileInfoAsync(
        string path,
        TemplateContext context,
        CancellationToken cancellationToken)
    {
        await Task.Yield();
        LastCancellationToken = cancellationToken;
        cancellationToken.ThrowIfCancellationRequested();

        path = NormalizePath(path);
        RequestedPaths.Add(path);

        if (!_sources.TryGetValue(path, out var entry))
        {
            return null;
        }

        return new TemplateSourceInfo(entry.LastModified, async cancellationToken =>
        {
            await Task.Yield();
            cancellationToken.ThrowIfCancellationRequested();
            _readCounts[path] = GetReadCount(path) + 1;
            return new MemoryStream(Encoding.UTF8.GetBytes(entry.Content));
        });
    }

    private static string NormalizePath(string path) => path.Replace('\\', '/').TrimStart('/');
}
