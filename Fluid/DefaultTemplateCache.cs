using Microsoft.Extensions.FileProviders;
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
    record struct CachedTemplate(DateTimeOffset LastModified, IFluidTemplate Template);

    private readonly ConcurrentDictionary<string, CachedTemplate> _cache;

    public TemplateCache()
    {
        // Use case-insensitive comparison only on Windows. Create a dedicated cache entry in other cases, even
        // on MacOS when the file system coulb be case-sensitive too.

        _cache = new(RuntimeInformation.IsOSPlatform(OSPlatform.Windows) ? StringComparer.OrdinalIgnoreCase : StringComparer.Ordinal);
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
