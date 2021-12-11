namespace System.Text
{
    internal static class StringBuilderExtensions
    {
        public static StringBuilder Trim(this StringBuilder value, char trimChar)
        {
            if (value is null)
            {
                throw new ArgumentNullException(nameof(value));
            }

            if (value.Length == 0)
            {
                return value;
            }

            int i = 0;

            for (; i <= value.Length - 1; i++)
            {
                if (value[i] != trimChar)
                {
                    break;
                }
            }

            if (i < value.Length)
            {
                value.Remove(0, i);
            }

            i = value.Length - 1;

            for (; i >= 0; i--)
            {
                if (value[i] != trimChar)
                {
                    break;
                }
            }

            if (i < value.Length - 1)
            {
                value.Length = i + 1;
            }

            return value;
        }
    }
}
