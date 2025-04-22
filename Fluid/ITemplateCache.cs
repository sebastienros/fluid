using Microsoft.Extensions.FileProviders;

namespace Fluid;

/// <summary>
/// Provides methods to retrieve and store templates using a <see cref="IFileInfo" />.
/// </summary>
public interface ITemplateCache
{
    bool TryGetTemplate(IFileInfo fileInfo, out IFluidTemplate template);

    void SetTemplate(IFileInfo fileInfo, IFluidTemplate template);
}
