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
            // Converting a name allocates whenever it isn't already camel-cased, and two names that are
            // ordinally equal always convert to the same result, so settle those without converting.
            if (ReferenceEquals(x, y))
            {
                return true;
            }

            if (x is null || y is null)
            {
                return false;
            }

            if (string.Equals(x, y, StringComparison.Ordinal))
            {
                return true;
            }

            // Camel-casing never changes the length of a name.
            if (x.Length != y.Length)
            {
                return false;
            }

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
            // Two ordinally equal names always convert to the same result, so skip the conversion,
            // which allocates.
            if (ReferenceEquals(x, y))
            {
                return true;
            }

            if (x is null || y is null)
            {
                return false;
            }

            if (string.Equals(x, y, StringComparison.Ordinal))
            {
                return true;
            }

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
