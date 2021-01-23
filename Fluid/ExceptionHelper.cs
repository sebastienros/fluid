#nullable enable

using System;
using System.Diagnostics.CodeAnalysis;
using System.Runtime.CompilerServices;

namespace Fluid
{
    internal static class ExceptionHelper
    {
        [DoesNotReturn]
        [MethodImpl(MethodImplOptions.NoInlining)]
        public static void ThrowArgumentNullException(string paramName, string? message = null)
        {
            throw new ArgumentNullException(paramName, message);
        }

        [DoesNotReturn]
        [MethodImpl(MethodImplOptions.NoInlining)]
        public static void ThrowArgumentOutOfRangeException(string paramName, string message)
        {
            throw new ArgumentOutOfRangeException(paramName, message);
        }

        [DoesNotReturn]
        [MethodImpl(MethodImplOptions.NoInlining)]
        public static void ThrowParseException<T>(string message)
        {
            throw new ParseException(message);
        }

#if NETSTANDARD2_0
        private class DoesNotReturnAttribute : Attribute {}
#endif

    }
}