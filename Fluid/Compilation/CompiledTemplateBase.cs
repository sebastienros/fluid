using Fluid.Values;
using System.Runtime.CompilerServices;
using System.Text.Encodings.Web;

namespace Fluid.Compilation
{
    public class CompiledTemplateBase
    {
        // Used by a compiled RangeExpression to create a FluidValueArray
        protected static FluidValue BuildRangeArray(int start, int end)
        {
            // If end < start, we create an empty array
            var list = new FluidValue[Math.Max(0, end - start + 1)];

            for (var i = 0; i < list.Length; i++)
            {
                list[i] = NumberValue.Create(start + i);
            }

            return new ArrayValue(list);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static Task WriteAsync(string value, TextWriter writer, TextEncoder encoder, TemplateContext _)
        {
            return writer.WriteAsync(encoder.Encode(value));
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static Task WriteAsync(int value, TextWriter writer, TextEncoder encoder, TemplateContext context)
        {
            return writer.WriteAsync(encoder.Encode(value.ToString(context.CultureInfo)));
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static Task WriteAsync(float value, TextWriter writer, TextEncoder encoder, TemplateContext context)
        {
            return writer.WriteAsync(encoder.Encode(value.ToString(context.CultureInfo)));
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static Task WriteAsync(double value, TextWriter writer, TextEncoder encoder, TemplateContext context)
        {
            return writer.WriteAsync(encoder.Encode(value.ToString(context.CultureInfo)));
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static Task WriteAsync(decimal value, TextWriter writer, TextEncoder encoder, TemplateContext context)
        {
            return writer.WriteAsync(encoder.Encode(value.ToString(context.CultureInfo)));
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static Task WriteAsync(bool value, TextWriter writer, TextEncoder _, TemplateContext __)
        {
            return writer.WriteAsync(value ? "true" : "false");
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static Task WriteAsync(DateTime value, TextWriter writer, TextEncoder _, TemplateContext context)
        {
            return writer.WriteAsync(value.ToString("u", context.CultureInfo));
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static Task WriteAsync(DateTimeOffset value, TextWriter writer, TextEncoder _, TemplateContext context)
        {
            return writer.WriteAsync(value.ToString("u", context.CultureInfo));
        }

        // TODO: Add other overloads for Write()

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static ValueTask WriteAsync(object value, TextWriter writer, TextEncoder encoder, TemplateContext context)
        {
            var wrapped = FluidValue.Create(value, context.Options);
            return wrapped.WriteToAsync(writer, encoder, context.CultureInfo);
        }

        protected static void InitializeFunctionArguments(TemplateContext context, FunctionArguments defaultArguments, FunctionArguments arguments)
        {
            // Set default values

            foreach (var name in defaultArguments.Names)
            {
                context.SetValue(name, defaultArguments[name]);
            }

            var namedArguments = false;

            // Apply all arguments from the invocation.

            var i = 0;

            foreach (var name in defaultArguments.Names)
            {
                // As soon as a named argument is used, all subsequent ones need a name too.
                namedArguments |= arguments.HasNamed(name);

                if (!namedArguments)
                {
                    if (arguments.Count > i)
                    {
                        context.SetValue(name, arguments.At(i));
                    }
                }
                else
                {
                    context.SetValue(name, arguments[name]);
                }

                i++;
            }
        }
    }
}
