//Selz: Helper method to convert string to snakecase for the member name etc
using System.Linq;

namespace Fluid
{
    public static class ExtensionMethods {
        public static string ToSnakeCase(this string str) {
            return string.Concat(str.Select((x, i) => i > 0 && char.IsUpper(x) ? "_" + x.ToString() : x.ToString())).ToLower();
        }
    }
}
