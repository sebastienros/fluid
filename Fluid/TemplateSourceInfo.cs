namespace Fluid;

/// <summary>
/// Represents a template source that can be read asynchronously.
/// </summary>
public sealed class TemplateSourceInfo
{
    private readonly Func<CancellationToken, ValueTask<Stream>> _openReadAsync;

    /// <summary>
    /// Initializes a new instance of <see cref="TemplateSourceInfo"/>.
    /// </summary>
    /// <param name="lastModified">The last modification date used to invalidate parsed templates.</param>
    /// <param name="openReadAsync">A delegate that asynchronously opens the template content.</param>
    /// <param name="cacheKey">
    /// An optional cache key that uniquely identifies this source across rendering contexts.
    /// The requested path is used when this value is <see langword="null"/>.
    /// </param>
    public TemplateSourceInfo(
        DateTimeOffset lastModified,
        Func<CancellationToken, ValueTask<Stream>> openReadAsync,
        string cacheKey = null)
    {
        ArgumentNullException.ThrowIfNull(openReadAsync);

        LastModified = lastModified;
        _openReadAsync = openReadAsync;
        CacheKey = cacheKey;
    }

    /// <summary>
    /// Gets the last modification date used to invalidate parsed templates.
    /// </summary>
    public DateTimeOffset LastModified { get; }

    /// <summary>
    /// Gets the cache key that uniquely identifies this source across rendering contexts.
    /// </summary>
    public string CacheKey { get; }

    /// <summary>
    /// Asynchronously opens the template content.
    /// </summary>
    /// <param name="cancellationToken">The token used to cancel the asynchronous operation.</param>
    public ValueTask<Stream> OpenReadAsync(CancellationToken cancellationToken = default) =>
        _openReadAsync(cancellationToken);

    /// <summary>
    /// Asynchronously reads the template content.
    /// </summary>
    /// <param name="cancellationToken">The token used to cancel the asynchronous operation.</param>
    public async ValueTask<string> ReadToEndAsync(CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();

        using var stream = await OpenReadAsync(cancellationToken);
        using var reader = new StreamReader(stream);
#if NET8_0_OR_GREATER
        return await reader.ReadToEndAsync(cancellationToken);
#else
        using var registration = cancellationToken.Register(static state => ((Stream) state).Dispose(), stream);

        try
        {
            var content = await reader.ReadToEndAsync();
            cancellationToken.ThrowIfCancellationRequested();
            return content;
        }
        catch (ObjectDisposedException) when (cancellationToken.IsCancellationRequested)
        {
            throw new OperationCanceledException(cancellationToken);
        }
        catch (IOException) when (cancellationToken.IsCancellationRequested)
        {
            throw new OperationCanceledException(cancellationToken);
        }
#endif
    }
}
