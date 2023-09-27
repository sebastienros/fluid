using Fluid.Values;
using System.Linq;

namespace Fluid.Tests.Extensions
{
    internal static class ConversionExtensions
    {
        public static FilterArguments ToFilterArguments(this object[] arguments)
        {
            return new FilterArguments(arguments.Select(x => FluidValue.Create(x, TemplateOptions.Default)).ToArray());
        }
    }
}
