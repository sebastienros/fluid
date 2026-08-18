namespace Fluid;

/// <summary>
/// Exposes a version for an <see cref="ITemplateFileProvider"/> that changes whenever resolving any
/// template could return a different source.
/// </summary>
/// <remarks>
/// Implementations must publish source changes before incrementing <see cref="Version"/>. Source state
/// and the corresponding version must be safe to read concurrently.
/// </remarks>
public interface IVersionedTemplateFileProvider : ITemplateFileProvider
{
    /// <summary>
    /// Gets the current version of the template sources exposed by this provider.
    /// </summary>
    long Version { get; }

    /// <summary>
    /// Gets a stable identity for the set of sources visible to the specified context.
    /// </summary>
    /// <remarks>
    /// Return the same object instance for contexts that resolve paths identically. Return <see langword="null"/>
    /// to disable render resolution caching for the specified context.
    /// </remarks>
    object GetTemplateResolutionCacheKey(TemplateContext context);
}
