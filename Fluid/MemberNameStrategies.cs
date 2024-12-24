#if !NET6_0_OR_GREATER
using System.Text;
#endif
namespace Fluid
{
    public sealed class MemberNameStrategies
    {
        public static readonly MemberNameStrategy Default = RenameDefault;
        public static readonly MemberNameStrategy IgnoreCase = RenameIgnoreCase;
        public static readonly MemberNameStrategy CamelCase = RenameCamelCase;
        public static readonly MemberNameStrategy SnakeCase = RenameSnakeCase;

        private static string RenameDefault(string memberName) => memberName;

        private static string RenameIgnoreCase(string memberName) => memberName.ToLowerInvariant();

#if NET6_0_OR_GREATER
        public static string RenameCamelCase(string memberName)
        {
            return String.Create(memberName.Length, memberName, (data, name) =>
            {
                data[0] = char.ToLowerInvariant(name[0]);
                name.AsSpan().Slice(1).CopyTo(data.Slice(1));
            });
        }

        public static string RenameSnakeCase(string memberName)
        {
            var upper = 0;
            for (var i = 1; i < memberName.Length; i++)
            {
                if (char.IsUpper(memberName[i]))
                {
                    upper++;
                }
            }

            return String.Create(memberName.Length + upper, memberName, (data, name) =>
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
        public static string RenameCamelCase(string memberName)
        {
            var firstChar = memberName[0];

            if (firstChar == char.ToLowerInvariant(firstChar))
            {
                return memberName;
            }

            var name = memberName.ToCharArray();
            name[0] = char.ToLowerInvariant(firstChar);

            return new String(name);
        }

        public static string RenameSnakeCase(string memberName)
        {
            var builder = new StringBuilder();
            var previousUpper = false;

            for (var i = 0; i < memberName.Length; i++)
            {
                var c = memberName[i];
                if (char.IsUpper(c))
                {
                    if (i > 0 && !previousUpper)
                    {
                        builder.Append('_');
                    }
                    builder.Append(char.ToLowerInvariant(c));
                    previousUpper = true;
                }
                else
                {
                    builder.Append(c);
                    previousUpper = false;
                }
            }
            return builder.ToString();
        }
#endif
    }
}
