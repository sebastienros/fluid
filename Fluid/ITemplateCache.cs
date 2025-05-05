using System.Diagnostics.CodeAnalysis;

namespace Fluid;

/// <summary>
/// Interface for caching parsed templates in memory.
/// </summary>
[Experimental("FLUID001")]
public interface ITemplateCache
{
    /// <summary>
    /// Attempts to retrieve a cached template based on the provided subpath.
    /// </summary>
    /// <param name="subpath">The relative path that identifies the file.</param>
    /// <param name="lastModified">The last modified time of the template file.</param>
    /// <param name="template">The cached template if found.</param>
    /// <returns>True if the template is found in the cache; otherwise, false.</returns>
    bool TryGetTemplate(string subpath, DateTimeOffset lastModified, out IFluidTemplate template);

    /// <summary>
    /// Stores a template in the cache with the specified subpath as the key.
    /// </summary>
    /// <param name="subpath">The relative path that identifies the file.</param>
    /// <param name="lastModified">The last modified time of the template file.</param>
    /// <param name="template">The template to store in the cache.</param>
    void SetTemplate(string subpath, DateTimeOffset lastModified, IFluidTemplate template);
}
