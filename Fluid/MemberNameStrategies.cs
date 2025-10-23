using System.Text;

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
            return RenameCamelCase(x).CompareTo(y);
        }
        public override bool Equals(string x, string y)
        {
            return RenameCamelCase(x).Equals(y, StringComparison.OrdinalIgnoreCase);
        }
        public override int GetHashCode(string obj)
        {
            return StringComparer.OrdinalIgnoreCase.GetHashCode(obj);
        }

#if NET6_0_OR_GREATER
        public static string RenameCamelCase(string name)
        {
            if (string.IsNullOrWhiteSpace(name))
            {
                return string.Empty;
            }

            return String.Create(name.Length, name, (data, name) =>
            {
                data[0] = char.ToLowerInvariant(name[0]);
                name.AsSpan().Slice(1).CopyTo(data.Slice(1));
            });
        }
#else
        public static string RenameCamelCase(string name)
        {
            if (string.IsNullOrWhiteSpace(name))
            {
                return string.Empty;
            }

            var firstChar = name[0];

            if (firstChar == char.ToLowerInvariant(firstChar))
            {
                return name;
            }

            var chars = name.ToCharArray();
            chars[0] = char.ToLowerInvariant(firstChar);

            return new string(chars);
        }
#endif
    }

    public sealed class SnakeCaseStringComparer : StringComparer
    {
        public override int Compare(string x, string y)
        {
            return RenameSnakeCase(x).CompareTo(y);
        }
        public override bool Equals(string x, string y)
        {
            return RenameSnakeCase(x).Equals(y, StringComparison.OrdinalIgnoreCase);
        }
        public override int GetHashCode(string obj)
        {
            return StringComparer.OrdinalIgnoreCase.GetHashCode(obj);
        }

#if NET6_0_OR_GREATER
        public static string RenameSnakeCase(string name)
        {
            if (string.IsNullOrWhiteSpace(name))
            {
                return string.Empty;
            }

            var upper = 0;
            for (var i = 1; i < name.Length; i++)
            {
                if (char.IsUpper(name[i]))
                {
                    upper++;
                }
            }

            return string.Create(name.Length + upper, name, (data, name) =>
            {
                var previousUpper = false;
                var k = 0;

                for (var i = 0; i < name.Length; i++)
                {
                    var c = name[i];
                    if (char.IsUpper(c))
                    {
                        if (i > 0 && !previousUpper)
                        {
                            data[k++] = '_';
                        }
                        data[k++] = char.ToLowerInvariant(c);
                        previousUpper = true;
                    }
                    else
                    {
                        data[k++] = c;
                        previousUpper = false;
                    }
                }
            });
        }
#else
        public static string RenameSnakeCase(string name)
        {
            var input = name;

            if (string.IsNullOrWhiteSpace(input))
            {
                return string.Empty;
            }

            var result = new StringBuilder();
            bool wasPrevUpper = false; // Track if the previous character was uppercase
            int uppercaseCount = 0; // Count consecutive uppercase letters at the start

            for (int i = 0; i < input.Length; i++)
            {
                char c = input[i];

                if (char.IsUpper(c))
                {
                    if (i > 0 && (!wasPrevUpper || (uppercaseCount > 1 && i < input.Length - 1 && char.IsLower(input[i + 1]))))
                    {
                        result.Append('_');
                    }

                    result.Append(char.ToLower(c));
                    wasPrevUpper = true;
                    uppercaseCount++;
                }
                else
                {
                    if (c == ' ' || c == '-')
                    {
                        result.Append('_'); // Replace spaces and hyphens with underscores
                    }
                    else
                    {
                        result.Append(c);
                    }

                    wasPrevUpper = false;
                }
            }

            return result.ToString();
        }
#endif
    }
}
