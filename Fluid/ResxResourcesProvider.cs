using System.Globalization;
using System.Reflection;
using System.Resources;
using System.Threading.Tasks;

namespace Fluid
{
    /// <summary>
    /// <see cref="IResourcesProvider"/> implementation to load resource strings from standard .NET .resx files.
    /// </summary>
    public class ResxResourcesProvider : IResourcesProvider
    {
        /// <summary>
        /// Initialises a new <see cref="ResxResourcesProvider"/>.
        /// </summary>
        /// <param name="baseName">The root name of the resource file without its extension but including any
        /// fully qualified namespace name. For example, the root name for the resource file
        /// 'MyApplication.MyResources.en-US.resx' is 'MyApplication.MyResources'.</param>
        /// <param name="assembly">The main assembly for the resources.</param>
        /// <remarks>For performance reasons, create a single instance of this class for a given resource
        /// bundle for your application.</remarks>
        public ResxResourcesProvider(string baseName, Assembly assembly)
        {
            _resourceManager = new ResourceManager(baseName, assembly);
        }

        private readonly ResourceManager _resourceManager;

        /// <inheritdoc />
        public ValueTask<string> GetString(string name, CultureInfo culture) =>
            new ValueTask<string>(_resourceManager.GetString(name, culture));
    }
}