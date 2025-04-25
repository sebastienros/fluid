using Microsoft.Extensions.FileProviders;
using System.Collections.Concurrent;

namespace Fluid;

/// <summary>
/// This implementation of <see cref="ITemplateCache"/> caches templates in memory.
/// It uses a <see cref="ConcurrentDictionary{TKey, TValue}"/> to store the templates.
/// If the template file is modified, the cache entry is removed.
/// </summary>
sealed class TemplateCache : ITemplateCache
{
    record struct CachedTemplate(DateTimeOffset LastModified, IFluidTemplate Template);

    private readonly ConcurrentDictionary<string, CachedTemplate> _cache;

    public TemplateCache()
    {
        // On case-sensitive file systems (like Linux), we use Ordinal comparison.
        var comparison =
            Environment.OSVersion.Platform == PlatformID.Unix ||
            Environment.OSVersion.Platform == PlatformID.MacOSX
            ? StringComparer.Ordinal : StringComparer.OrdinalIgnoreCase;

        _cache = new(comparison);
    }

    public bool TryGetTemplate(IFileInfo fileInfo, out IFluidTemplate template)
    {
        template = null;

        if (_cache.TryGetValue(fileInfo.PhysicalPath, out var cachedTemplate))
        {
            if (cachedTemplate.LastModified < fileInfo.LastModified)
            {
                // The template has been modified, so we can remove it from the cache
                _cache.TryRemove(fileInfo.PhysicalPath, out _);

                return false;
            }
            else
            {
                // Return the cached template if it is still valid
                template = cachedTemplate.Template;
                return true;
            }
        }

        return false;
    }

    public void SetTemplate(IFileInfo fileInfo, IFluidTemplate template)
    {
        var cachedTemplate = new CachedTemplate(fileInfo.LastModified, template);
        _cache[fileInfo.PhysicalPath] = cachedTemplate;
    }
}
