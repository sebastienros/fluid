using Microsoft.Extensions.FileProviders;

namespace Fluid;

/// <summary>
/// Adapts an <see cref="IFileProvider"/> to <see cref="ITemplateFileProvider"/>.
/// </summary>
public sealed class FileProviderTemplateFileProvider : ITemplateFileProvider
{
    private readonly IFileProvider _fileProvider;

    public FileProviderTemplateFileProvider(IFileProvider fileProvider)
    {
        ArgumentNullException.ThrowIfNull(fileProvider);
        _fileProvider = fileProvider;
    }

    public ValueTask<TemplateSourceInfo> GetFileInfoAsync(
        string subpath,
        TemplateContext context,
        CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();

        var fileInfo = _fileProvider.GetFileInfo(subpath);
        if (fileInfo == null || !fileInfo.Exists || fileInfo.IsDirectory)
        {
            return default;
        }

        return new ValueTask<TemplateSourceInfo>(
            new TemplateSourceInfo(
                fileInfo.LastModified,
                _ => new ValueTask<Stream>(fileInfo.CreateReadStream())));
    }
}
