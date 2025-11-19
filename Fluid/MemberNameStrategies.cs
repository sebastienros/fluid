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
            var cx = JsonNamingPolicy.CamelCase.ConvertName(x);
            var cy = JsonNamingPolicy.CamelCase.ConvertName(y);
            return string.Compare(cx, cy, StringComparison.Ordinal);
        }

        public override bool Equals(string x, string y)
        {
            var cx = JsonNamingPolicy.CamelCase.ConvertName(x);
            var cy = JsonNamingPolicy.CamelCase.ConvertName(y);
            return string.Equals(cx, cy, StringComparison.Ordinal);
        }
    
        public override int GetHashCode(string obj)
        {
            return JsonNamingPolicy.CamelCase.ConvertName(obj).GetHashCode();
        }
    }

    public sealed class SnakeCaseStringComparer : StringComparer
    {
        public override int Compare(string x, string y)
        {
            var cx = JsonNamingPolicy.SnakeCaseLower.ConvertName(x);
            var cy = JsonNamingPolicy.SnakeCaseLower.ConvertName(y);
            return string.Compare(cx, cy, StringComparison.Ordinal);
        }

        public override bool Equals(string x, string y)
        {
            var cx = JsonNamingPolicy.SnakeCaseLower.ConvertName(x);
            var cy = JsonNamingPolicy.SnakeCaseLower.ConvertName(y);
            return string.Equals(cx, cy, StringComparison.Ordinal);
        }

        public override int GetHashCode(string obj)
        {
            return JsonNamingPolicy.SnakeCaseLower.ConvertName(obj).GetHashCode();
        }
    }
}
