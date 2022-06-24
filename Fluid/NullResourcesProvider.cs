using System.Globalization;
using System.Threading.Tasks;

namespace Fluid
{
    /// <summary>
    /// Default implementation of <see cref="IResourcesProvider"/> that simply returns the resource name
    /// as the value.
    /// </summary>
    public class NullResourcesProvider : IResourcesProvider
    {
        /// <inheritdoc />
        public ValueTask<string> GetString(string name, CultureInfo culture) => new ValueTask<string>(name);
    }
}
