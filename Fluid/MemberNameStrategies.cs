using System;
using System.Reflection;
using System.Text;

namespace Fluid
{
    public class MemberNameStrategies
    {
        public static readonly MemberNameStrategy Default = RenameDefault;
        public static readonly MemberNameStrategy CamelCase = RenameCamelCase;
        public static readonly MemberNameStrategy SnakeCase = RenameSnakeCase;

        private static string RenameDefault(MemberInfo member) => member.Name;

#if NETSTANDARD2_0
        public static string RenameCamelCase(MemberInfo member)
        {
            var firstChar = member.Name[0];

            if (firstChar == char.ToLowerInvariant(firstChar))
            {
                return member.Name;
            }

            var name = member.Name.ToCharArray();
            name[0] = char.ToLowerInvariant(firstChar);

            return new String(name);
        }

        public static string RenameSnakeCase(MemberInfo member)
        {
            var builder = new StringBuilder();
            var name = member.Name;
            var previousUpper = false;

            for (var i = 0; i < name.Length; i++)
            {
                var c = name[i];
                if (char.IsUpper(c))
                {
                    if (i > 0 && !previousUpper)
                    {
                        builder.Append("_");
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
#else
        public static string RenameCamelCase(MemberInfo member)
        {
            return String.Create(member.Name.Length, member.Name, (data, name) =>
            {
                data[0] = char.ToLowerInvariant(name[0]);
                name.AsSpan().Slice(1).CopyTo(data.Slice(1));
            });
        }

        public static string RenameSnakeCase(MemberInfo member)
        {
            var upper = 0;
            for (var i = 1; i < member.Name.Length; i++)
            {
                if (char.IsUpper(member.Name[i]))
                {
                    upper++;
                }
            }

            return String.Create(member.Name.Length + upper, member.Name, (data, name) =>
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
#endif
    }
}
