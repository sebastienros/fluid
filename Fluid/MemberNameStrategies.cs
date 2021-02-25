using System.Reflection;
using System.Text;

namespace Fluid
{
    public class MemberNameStrategies
    {
        public static readonly MemberNameStrategy Default = RenameDefault;
        public static readonly MemberNameStrategy CamelCase = RenameCamelCase;
        public static readonly MemberNameStrategy SnakeCase = RenameSnake;

        private static string RenameDefault(MemberInfo member) => member.Name;

        public static string RenameCamelCase(MemberInfo member)
        {
            var name = member.Name;
            var firstChar = name[0];

            if (firstChar == char.ToLowerInvariant(firstChar))
            {
                return name;
            }

            return char.ToLowerInvariant(firstChar) + name.Substring(1);
        }

        public static string RenameSnake(MemberInfo member)
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
    }
}
