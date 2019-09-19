using System;
using System.Runtime.CompilerServices;

namespace Fluid
{
    internal static class ExceptionHelper
    {
        [MethodImpl(MethodImplOptions.NoInlining)]
        public static void ThrowArgumentNullException(string paramName)
        {
            ThrowArgumentNullException<object>(paramName);
        }

        [MethodImpl(MethodImplOptions.NoInlining)]
        public static T ThrowArgumentNullException<T>(string paramName)
        {
            throw new ArgumentNullException(paramName);
        }

        [MethodImpl(MethodImplOptions.NoInlining)]
        public static T ThrowParseException<T>(string message)
        {
            throw new ParseException(message);
        }
    }
}