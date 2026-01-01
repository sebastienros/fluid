using Fluid.Ast;
using Fluid.Utils;
using Fluid.Values;
using System.Globalization;
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
            if (statement == null)
            {
                ExceptionHelper.ThrowArgumentNullException(nameof(statement));
            }

            if (writer == null)
            {
                ExceptionHelper.ThrowArgumentNullException(nameof(writer));
            }

            if (encoder == null)
            {
                ExceptionHelper.ThrowArgumentNullException(nameof(encoder));
            }

            if (context == null)
            {
                ExceptionHelper.ThrowArgumentNullException(nameof(context));
            }

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
            if (template == null)
            {
                ExceptionHelper.ThrowArgumentNullException(nameof(template));
            }

            if (writer == null)
            {
                ExceptionHelper.ThrowArgumentNullException(nameof(writer));
            }

            if (encoder == null)
            {
                ExceptionHelper.ThrowArgumentNullException(nameof(encoder));
            }

            if (context == null)
            {
                ExceptionHelper.ThrowArgumentNullException(nameof(context));
            }

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
            if (value == null)
            {
                ExceptionHelper.ThrowArgumentNullException(nameof(value));
            }

            if (writer == null)
            {
                ExceptionHelper.ThrowArgumentNullException(nameof(writer));
            }

            if (encoder == null)
            {
                ExceptionHelper.ThrowArgumentNullException(nameof(encoder));
            }

            if (cultureInfo == null)
            {
                ExceptionHelper.ThrowArgumentNullException(nameof(cultureInfo));
            }

            using var output = new TextWriterFluidOutput(writer, bufferSize: 16 * 1024, leaveOpen: true);
            await value.WriteToAsync(output, encoder, cultureInfo);
            await output.FlushAsync();
        }
    }
}
