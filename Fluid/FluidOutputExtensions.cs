using Fluid.Ast;
using Fluid.Utils;
using Fluid.Values;
using System.Buffers;
using System.Globalization;
using System.Runtime.CompilerServices;
using System.Text.Encodings.Web;

namespace Fluid
{
    /// <summary>
    /// Back-compat extension methods that adapt legacy TextWriter-based rendering to <see cref="IFluidOutput"/>.
    /// </summary>
    public static class FluidOutputExtensions
    {
        public static async ValueTask<Completion> WriteToAsync(this Statement statement, TextWriter writer, TextEncoder encoder, TemplateContext context)
        {
            ArgumentNullException.ThrowIfNull(statement);
            ArgumentNullException.ThrowIfNull(writer);
            ArgumentNullException.ThrowIfNull(encoder);
            ArgumentNullException.ThrowIfNull(context);

            var bufferSize = context.Options?.OutputBufferSize ?? 16 * 1024;
            if (bufferSize <= 0)
            {
                bufferSize = 16 * 1024;
            }

            using var output = new TextWriterFluidOutput(writer, bufferSize, leaveOpen: true);
            var completion = await statement.WriteToAsync(output, encoder, context);
            await output.FlushAsync();
            return completion;
        }

        public static async ValueTask RenderAsync(this IFluidTemplate template, TextWriter writer, TextEncoder encoder, TemplateContext context)
        {
            ArgumentNullException.ThrowIfNull(template);
            ArgumentNullException.ThrowIfNull(writer);
            ArgumentNullException.ThrowIfNull(encoder);
            ArgumentNullException.ThrowIfNull(context);

            var bufferSize = context.Options?.OutputBufferSize ?? 16 * 1024;
            if (bufferSize <= 0)
            {
                bufferSize = 16 * 1024;
            }

            using var output = new TextWriterFluidOutput(writer, bufferSize, leaveOpen: true);
            await template.RenderAsync(output, encoder, context);
            await output.FlushAsync();
        }

        public static async ValueTask WriteToAsync(this FluidValue value, TextWriter writer, TextEncoder encoder, CultureInfo cultureInfo)
        {
            ArgumentNullException.ThrowIfNull(value);
            ArgumentNullException.ThrowIfNull(writer);
            ArgumentNullException.ThrowIfNull(encoder);
            ArgumentNullException.ThrowIfNull(cultureInfo);

            using var output = new TextWriterFluidOutput(writer, bufferSize: 16 * 1024, leaveOpen: true);
            await value.WriteToAsync(output, encoder, cultureInfo);
            await output.FlushAsync();
        }

        internal static void Write(this IFluidOutput output, TextEncoder encoder, string value)
        {
            if (string.IsNullOrEmpty(value))
            {
                return;
            }

            if (encoder is NullEncoder)
            {
                output.Write(value);
                return;
            }

            #if NET8_0_OR_GREATER
            unsafe
            {
                fixed (char* pValue = value)
                {
                    // If nothing needs encoding, write directly.
                    if (encoder.FindFirstCharacterToEncode(pValue, value.Length) == -1)
                    {
                        output.Write(value);
                        return;
                    }
                }
            }

            ReadOnlySpan<char> remaining = value.AsSpan();

            // Ensure we can encode at least one scalar without getting stuck.
            var minRequired = Math.Max(1, encoder.MaxOutputCharactersPerInputCharacter);

            while (!remaining.IsEmpty)
            {
                Span<char> destination;

                try
                {
                    destination = output.GetSpan(minRequired);
                }
                catch
                {
                    // Some bounded outputs may reject size hints larger than their internal buffer.
                    // Fall back to allocating the encoded string.
                    output.Write(encoder.Encode(remaining.ToString()));
                    return;
                }

                if (destination.Length < minRequired)
                {
                    // Can't guarantee progress with span encoding; fall back.
                    output.Write(encoder.Encode(remaining.ToString()));
                    return;
                }

                var status = encoder.Encode(remaining, destination, out var charsConsumed, out var charsWritten, isFinalBlock: true);

                if (charsWritten > 0)
                {
                    output.Advance(charsWritten);
                }

                if (charsConsumed > 0)
                {
                    remaining = remaining.Slice(charsConsumed);
                }
                else if (charsWritten == 0)
                {
                    // Safety valve: avoid infinite loops if an encoder reports no progress.
                    output.Write(encoder.Encode(remaining.ToString()));
                    return;
                }

                if (status == OperationStatus.Done)
                {
                    return;
                }

                // For DestinationTooSmall, loop and request more space.
            }
            #else
            output.Write(encoder.Encode(value));
            #endif
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        internal static void Write(this IFluidOutput output, ReadOnlySpan<char> value)
        {
            if (value.IsEmpty)
            {
                return;
            }

            while (!value.IsEmpty)
            {
                var destination = output.GetSpan(1);
                if (destination.IsEmpty)
                {
                    // Defensive: should not happen, but avoid an infinite loop.
                    return;
                }

                var toCopy = value.Length < destination.Length ? value.Length : destination.Length;
                value.Slice(0, toCopy).CopyTo(destination);
                output.Advance(toCopy);
                value = value.Slice(toCopy);
            }
        }

        internal static void Write(this IFluidOutput output, TextEncoder encoder, ReadOnlySpan<char> value)
        {
            if (value.IsEmpty)
            {
                return;
            }

            if (encoder is NullEncoder)
            {
                output.Write(value);
                return;
            }

            #if NET8_0_OR_GREATER
            unsafe
            {
                fixed (char* pValue = value)
                {
                    if (encoder.FindFirstCharacterToEncode(pValue, value.Length) == -1)
                    {
                        output.Write(value);
                        return;
                    }
                }
            }

            // Ensure we can encode at least one scalar without getting stuck.
            var minRequired = Math.Max(1, encoder.MaxOutputCharactersPerInputCharacter);
            var remaining = value;

            while (!remaining.IsEmpty)
            {
                Span<char> destination;

                try
                {
                    destination = output.GetSpan(minRequired);
                }
                catch
                {
                    output.Write(encoder.Encode(remaining.ToString()));
                    return;
                }

                if (destination.Length < minRequired)
                {
                    output.Write(encoder.Encode(remaining.ToString()));
                    return;
                }

                var status = encoder.Encode(remaining, destination, out var charsConsumed, out var charsWritten, isFinalBlock: true);

                if (charsWritten > 0)
                {
                    output.Advance(charsWritten);
                }

                if (charsConsumed > 0)
                {
                    remaining = remaining.Slice(charsConsumed);
                }
                else if (charsWritten == 0)
                {
                    output.Write(encoder.Encode(remaining.ToString()));
                    return;
                }

                if (status == OperationStatus.Done)
                {
                    return;
                }
            }
            #else
            output.Write(encoder.Encode(value.ToString()));
            #endif
        }
    }
}
