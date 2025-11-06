using System.Text;
using System.Text.Json;

namespace Fluid
{
    public sealed class StringComparers
    {
        public static StringComparer CamelCase { get; } = new CamelCaseStringComparer();
        public static StringComparer SnakeCase { get; } = new SnakeCaseStringComparer();
    }

    public sealed class CamelCaseStringComparer : StringComparer
    {
        public override int Compare(string x, string y)
        {
            return JsonNamingPolicy.CamelCase.ConvertName(x).CompareTo(y);
        }
        public override bool Equals(string x, string y)
        {
            return JsonNamingPolicy.CamelCase.ConvertName(x).Equals(y, StringComparison.OrdinalIgnoreCase);
        }
        public override int GetHashCode(string obj)
        {
            return StringComparer.OrdinalIgnoreCase.GetHashCode(obj);
        }
    }

    public sealed class SnakeCaseStringComparer : StringComparer
    {
        public override int Compare(string x, string y)
        {
            return JsonNamingPolicy.SnakeCaseLower.ConvertName(x).CompareTo(y);
        }
        public override bool Equals(string x, string y)
        {
            return JsonNamingPolicy.SnakeCaseLower.ConvertName(x).Equals(y, StringComparison.OrdinalIgnoreCase);
        }
        public override int GetHashCode(string obj)
        {
            return StringComparer.OrdinalIgnoreCase.GetHashCode(obj);
        }
    }
}
