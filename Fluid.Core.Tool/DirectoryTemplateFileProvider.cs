namespace Fluid.Tool;

/// <summary>
/// Resolves include/render templates from a list of directories, in order.
/// </summary>
internal sealed class DirectoryTemplateFileProvider : ITemplateFileProvider
{
    private readonly string[] _roots;

    public DirectoryTemplateFileProvider(IEnumerable<string> roots)
    {
        _roots = roots.Select(Path.GetFullPath).ToArray();
    }

    public ValueTask<TemplateSourceInfo> GetFileInfoAsync(string subpath, TemplateContext context, CancellationToken cancellationToken)
    {
        foreach (var root in _roots)
        {
            var candidates = new[] { subpath, subpath + ".liquid" };
            foreach (var candidate in candidates)
            {
                var full = Path.GetFullPath(Path.Combine(root, candidate.TrimStart('/', '\\')));

                // Prevent escaping the include directory.
                if (!full.StartsWith(root.TrimEnd(Path.DirectorySeparatorChar) + Path.DirectorySeparatorChar, StringComparison.Ordinal))
                {
                    continue;
                }

                if (File.Exists(full))
                {
                    var info = new FileInfo(full);
                    return new ValueTask<TemplateSourceInfo>(new TemplateSourceInfo(
                        info.LastWriteTimeUtc,
                        _ => new ValueTask<Stream>(File.OpenRead(full)),
                        full));
                }
            }
        }

        return new ValueTask<TemplateSourceInfo>((TemplateSourceInfo)null!);
    }
}
