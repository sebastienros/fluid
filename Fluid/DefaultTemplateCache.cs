using System.Collections.Concurrent;
using System.Runtime.InteropServices;

namespace Fluid;

/// <summary>
/// This implementation of <see cref="ITemplateCache"/> caches templates in memory.
/// It uses a <see cref="ConcurrentDictionary{TKey, TValue}"/> to store the templates.
/// If the template file is modified, the cache entry is removed.
/// </summary>
sealed class TemplateCache : ITemplateCache
{
    private sealed record class TemplateCacheEntry(string Subpath, DateTimeOffset LastModified, IFluidTemplate Template);

    private readonly ConcurrentDictionary<string, TemplateCacheEntry> _cache;

    public TemplateCache()
    {
        // Use case-insensitive comparison only on Windows. Create a dedicated cache entry in other cases, even
        // on MacOS when the file system coulb be case-sensitive too.

        _cache = new(RuntimeInformation.IsOSPlatform(OSPlatform.Windows) ? StringComparer.OrdinalIgnoreCase : StringComparer.Ordinal);
    }

    public bool TryGetTemplate(string subpath, DateTimeOffset lastModified, out IFluidTemplate template)
    {
        template = default;

        if (_cache.TryGetValue(subpath, out var templateCacheEntry))
        {
            if (templateCacheEntry.LastModified < lastModified)
            {
                // The template has been modified, so we can remove it from the cache
                _cache.TryRemove(subpath, out _);

                return false;
            }
            else
            {
                template = templateCacheEntry.Template;
                return true;
            }
        }

        return false;
    }

    public void SetTemplate(string subpath, DateTimeOffset lastModified, IFluidTemplate template)
    {
        _cache[subpath] = new TemplateCacheEntry(subpath, lastModified, template);
    }
}
