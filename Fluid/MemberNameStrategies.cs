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
            // Calculate the exact number of underscores needed
            var underscores = 0;
            var previousUpper = false;
            
            for (var i = 0; i < member.Name.Length; i++)
            {
                var c = member.Name[i];
                if (char.IsUpper(c))
                {
                    if (i > 0 && (!previousUpper || (i + 1 < member.Name.Length && char.IsLower(member.Name[i + 1]))))
                    {
                        underscores++;
                    }
                    previousUpper = true;
                }
                else
                {
                    previousUpper = false;
                }
            }

            return String.Create(member.Name.Length + underscores, member.Name, (data, name) =>
            {
                previousUpper = false;
                var k = 0;

                for (var i = 0; i < name.Length; i++)
                {
                    var c = name[i];
                    if (char.IsUpper(c))
                    {
                        // Insert underscore if:
                        // 1. Not at the start (i > 0)
                        // 2. Either:
                        //    a. Previous char was not uppercase (transition from lowercase to uppercase)
                        //    b. Previous char was uppercase AND next char is lowercase (end of acronym, start of new word)
                        if (i > 0 && (!previousUpper || (i + 1 < name.Length && char.IsLower(name[i + 1]))))
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
            bool previousUpper = false;

            for (int i = 0; i < input.Length; i++)
            {
                char c = input[i];

                if (char.IsUpper(c))
                {
                    // Insert underscore if:
                    // 1. Not at the start (i > 0)
                    // 2. Either:
                    //    a. Previous char was not uppercase (transition from lowercase to uppercase)
                    //    b. Previous char was uppercase AND next char is lowercase (end of acronym, start of new word)
                    if (i > 0 && (!previousUpper || (i + 1 < input.Length && char.IsLower(input[i + 1]))))
                    {
                        result.Append('_');
                    }

                    result.Append(char.ToLower(c));
                    previousUpper = true;
                }
                else
                {
                    result.Append(c);
                    previousUpper = false;
                }
            }

            return result.ToString();
        }
#endif
    }
}
