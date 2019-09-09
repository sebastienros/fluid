using System.IO;
using System.Reflection;

namespace Fluid.Tests.Issues
{
    public class ResourceHelper
    {
        public static string GetEmbeddedResource(string resourceName, Assembly assembly)
        {
            using (var stream = assembly.GetManifestResourceStream(resourceName))
            using (var reader = new StreamReader(stream))
            {
                return reader.ReadToEnd();
            }
        }
    }
}