namespace Fluid;

/// <summary>
/// Provides asynchronous template files through a delegate.
/// </summary>
public sealed class DelegateTemplateFileProvider : ITemplateFileProvider
{
    private readonly Func<string, TemplateContext, CancellationToken, ValueTask<TemplateSourceInfo>> _getFileInfoAsync;

    public DelegateTemplateFileProvider(
        Func<string, TemplateContext, CancellationToken, ValueTask<TemplateSourceInfo>> getFileInfoAsync)
    {
        ArgumentNullException.ThrowIfNull(getFileInfoAsync);
        _getFileInfoAsync = getFileInfoAsync;
    }

    public ValueTask<TemplateSourceInfo> GetFileInfoAsync(
        string subpath,
        TemplateContext context,
        CancellationToken cancellationToken) =>
        _getFileInfoAsync(subpath, context, cancellationToken);
}
