#nullable enable

using System;
using System.Diagnostics.CodeAnalysis;
using System.Runtime.CompilerServices;

namespace Fluid;
static partial class Polyfill
{
    extension(ArgumentNullException)
    {
#if !NET6_0_OR_GREATER
        public static void ThrowIfNull([NotNull] object? argument, [CallerArgumentExpression(nameof(argument))] string? paramName = null)
        {
            if (argument is null)
            {
                throw new ArgumentNullException(paramName);
            }
        }
#endif
    }

    extension(ArgumentOutOfRangeException)
    {
#if !NET8_0_OR_GREATER
        public static void ThrowIfGreaterThan<T>(T value, T other, [CallerArgumentExpression(nameof(value))] string? paramName = null)
            where T : IComparable<T>
        {
            if (value.CompareTo(other) > 0)
            {
                throw new ArgumentOutOfRangeException(paramName, value, $"Value must be less than or equal to {other}.");
            }
        }
#endif

#if !NET8_0_OR_GREATER
        public static void ThrowIfNegative<T>(T value, [CallerArgumentExpression(nameof(value))] string? paramName = null)
#if NET7_0_OR_GREATER
            where T : INumberBase<T>
        {
            if (T.IsNegative(value))
            {
                ThrowNegative(value, paramName);
            }
        }
#else
            where T : struct, IComparable<T>
        {
            if (value.CompareTo(default(T)) < 0)
            {
                ThrowNegative(value, paramName);
            }
        }
#endif

#if !NET7_0_OR_GREATER
        public static void ThrowIfNegative(nint value, [CallerArgumentExpression(nameof(value))] string? paramName = null)
        {
            if (value < (nint) 0)
            {
                ThrowNegative(value, paramName);
            }
        }
#endif

        [DoesNotReturn]
        static void ThrowNegative<T>(T value, string? paramName) =>
            throw new ArgumentOutOfRangeException(paramName, value, "Value must be non-negative.");

#endif

    }
}

internal static class ExceptionHelper
{
    [DoesNotReturn]
    [MethodImpl(MethodImplOptions.NoInlining)]
    public static void ThrowInvalidOperationException(string message)
    {
        throw new InvalidOperationException(message);
    }

    [DoesNotReturn]
    [MethodImpl(MethodImplOptions.NoInlining)]
    public static void ThrowArgumentOutOfRangeException(string paramName, string message)
    {
        throw new ArgumentOutOfRangeException(paramName, message);
    }

    [DoesNotReturn]
    [MethodImpl(MethodImplOptions.NoInlining)]
    public static void ThrowArgumentException(string paramName, string message)
    {
        throw new ArgumentException(paramName, message);
    }

    [DoesNotReturn]
    [MethodImpl(MethodImplOptions.NoInlining)]
    public static void ThrowMaximumRecursionException()
    {
        throw new InvalidOperationException("The maximum level of recursion has been reached. Your script must have a cyclic include statement.");
    }
}
