namespace Fluid;

/// <summary>
/// Provides asynchronous access to template files.
/// </summary>
public interface ITemplateFileProvider
{
    /// <summary>
    /// Asynchronously resolves a template source.
    /// </summary>
    /// <param name="subpath">The relative path that identifies the template.</param>
    /// <param name="context">The context used for the current render.</param>
    /// <param name="cancellationToken">The token used to cancel the asynchronous operation.</param>
    /// <returns>The template source, or <see langword="null"/> when the path does not exist.</returns>
    ValueTask<TemplateSourceInfo> GetFileInfoAsync(
        string subpath,
        TemplateContext context,
        CancellationToken cancellationToken);
}
