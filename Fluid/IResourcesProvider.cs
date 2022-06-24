using System.Globalization;
using System.Threading.Tasks;

namespace Fluid
{
    /// <summary>
    /// Provider to lookup resource values (translations). Used by the 'translate' filter.
    /// </summary>
    public interface IResourcesProvider
    {
        /// <summary>
        /// Look up a resource value for a particular name in the specified culture.
        /// </summary>
        /// <param name="name">Name of the resource value to lookup</param>
        /// <param name="culture">If provided, looks up the resource value for the specified
        /// <see cref="CultureInfo"/>. If <c>null</c>, the current culture is used.</param>
        /// <returns>The resource value for the given <paramref name="name"/> or <c>null</c> if not found.</returns>
        ValueTask<string> GetString(string name, CultureInfo culture);
    }
}
