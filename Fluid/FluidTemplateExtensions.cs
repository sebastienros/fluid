using System.Runtime.CompilerServices;
using System.Text.Encodings.Web;
using Fluid.Utils;

namespace Fluid
{
    public static partial class FluidTemplateExtensions
    {
        /// <summary>
        /// Renders a Fluid template asynchronously to a specified text writer. It uses a default template context for
        /// rendering.
        /// </summary>
        /// <param name="template">Specifies the fluid template to be rendered.</param>
        /// <param name="textWriter">Defines the output destination for the rendered content.</param>
        /// <returns>Returns a ValueTask representing the asynchronous rendering operation.</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static ValueTask RenderAsync(this IFluidTemplate template, TextWriter textWriter)
        {
            return template.RenderAsync(textWriter, new TemplateContext());
        }

        /// <summary>
        /// Renders a Fluid template asynchronously to a specified text writer using a default encoder.
        /// </summary>
        /// <param name="template">Represents the template to be rendered.</param>
        /// <param name="textWriter">Used to write the rendered output of the template.</param>
        /// <param name="context">Provides the context for rendering the template.</param>
        /// <returns>Returns a ValueTask representing the asynchronous operation.</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static ValueTask RenderAsync(this IFluidTemplate template, TextWriter textWriter, TemplateContext context)
        {
            return template.RenderAsync(textWriter, context, NullEncoder.Default);
        }

        /// <summary>
        /// Renders a Fluid template asynchronously to a specified text writer using a given context and encoder.
        /// </summary>
        /// <param name="template">Specifies the fluid template to be rendered.</param>
        /// <param name="textWriter">Defines the output destination for the rendered content.</param>
        /// <param name="context">Provides the context in which the template is evaluated.</param>
        /// <param name="encoder">Handles the encoding of the output content.</param>
        /// <param name="isolateContext">Indicates whether to evaluate the template in a separate context scope.</param>
        /// <returns>Returns a ValueTask representing the asynchronous operation.</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static async ValueTask RenderAsync(this IFluidTemplate template, TextWriter textWriter, TemplateContext context, TextEncoder encoder, bool isolateContext = true)
        {
            ArgumentNullException.ThrowIfNull(textWriter);
            ArgumentNullException.ThrowIfNull(context);
            ArgumentNullException.ThrowIfNull(template);

            // A template is evaluated in a child scope such that the provided TemplateContext is immutable
            if (isolateContext)
            {
                context.EnterChildScope();
            }

            var bufferSize = context.Options?.OutputBufferSize ?? 0;
            if (bufferSize <= 0)
            {
                bufferSize = 16 * 1024;
            }

            await using var output = new TextWriterFluidOutput(textWriter, bufferSize, leaveOpen: true);

            try
            {
                await template.RenderAsync(output, encoder, context);
                await output.FlushAsync();
                await textWriter.FlushAsync();
            }
            finally
            {
                if (isolateContext)
                {
                    context.ReleaseScope();
                }
            }
        }
    }
}
