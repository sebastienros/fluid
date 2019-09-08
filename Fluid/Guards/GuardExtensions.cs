using System;

namespace Fluid.Guards
{
    public static class Guard
    {
        public static void AgainstNullOrEmptyArguments(FilterArguments argument, string customMessage)
        {
            if (argument == null || argument.Count == 0)
            {
                throw new ArgumentException(customMessage);
            }
        }
    }
}