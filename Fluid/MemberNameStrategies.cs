using System.Reflection;
using System.Text.Json;
using System.Text;

namespace Fluid
{
    public sealed class MemberNameStrategies
    {
        private static string RenameDefault(MemberInfo member) => member.Name;

        private const string SwitchName = "Fluid.UseLegacyMemberNameStrategies";

        public static readonly MemberNameStrategy Default = RenameDefault;
        public static readonly MemberNameStrategy CamelCase;
        public static readonly MemberNameStrategy SnakeCase;

        static MemberNameStrategies()
        {
            // STJ member name strategies are not compatible with the legacy strategies but are faster.
            // To retain backward compatibility users have to set the Fluid.UseLegacyMemberNameStrategies switch to true.
            // c.f. https://learn.microsoft.com/en-us/dotnet/fundamentals/runtime-libraries/system-appcontext

            if (AppContext.TryGetSwitch(SwitchName, out var flag) && flag == true)
            {
                CamelCase = RenameCamelCase;
                SnakeCase = RenameSnakeCase;
            }
            else
            {
                CamelCase = member => JsonNamingPolicy.CamelCase.ConvertName(member.Name);
                SnakeCase = member => JsonNamingPolicy.SnakeCaseLower.ConvertName(member.Name);
            }
        }

#if NET6_0_OR_GREATER

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
#else
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
            var input = member.Name;

            if (string.IsNullOrWhiteSpace(input))
                return string.Empty;

            StringBuilder result = new StringBuilder();
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
