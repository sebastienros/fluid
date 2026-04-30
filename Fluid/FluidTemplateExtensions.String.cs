using Fluid.Utils;
using System.Runtime.CompilerServices;
using System.Text.Encodings.Web;

namespace Fluid
{
    public static partial class FluidTemplateExtensions
    {
        /// <summary>
        /// Renders the specified Fluid template asynchronously using a new TemplateContext.
        /// </summary>
        /// <param name="template">The Fluid template to render.</param>
        /// <returns>A ValueTask that represents the asynchronous rendering operation. The task result contains the rendered template as a string.</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static ValueTask<string> RenderAsync(this IFluidTemplate template)
        {
            return RenderAsync(template, new TemplateContext());
        }

        /// <summary>
        /// Renders the specified Fluid template asynchronously using the provided context and a default text encoder.
        /// </summary>
        /// <param name="template">The Fluid template to render.</param>
        /// <param name="context">The context to use for rendering the template.</param>
        /// <returns>A ValueTask that represents the asynchronous rendering operation. The task result contains the rendered template as a string.</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static ValueTask<string> RenderAsync(this IFluidTemplate template, TemplateContext context)
        {
            return RenderAsync(template, context, NullEncoder.Default);
        }

        /// <summary>
        /// Renders the specified Fluid template asynchronously using the provided context, encoder, and isolation settings.
        /// </summary>
        /// <param name="template">The Fluid template to render.</param>
        /// <param name="context">The context to use for rendering the template.</param>
        /// <param name="encoder">The text encoder to use for encoding the output.</param>
        /// <param name="isolateContext">A boolean value indicating whether to isolate the context during rendering.</param>
        /// <returns>A ValueTask that represents the asynchronous rendering operation. The task result contains the rendered template as a string.</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static async ValueTask<string> RenderAsync(this IFluidTemplate template, TemplateContext context, TextEncoder encoder, bool isolateContext = true)
        {
            ArgumentNullException.ThrowIfNull(context);
            ArgumentNullException.ThrowIfNull(template);
            ArgumentNullException.ThrowIfNull(encoder);

            // Mirror the TextWriter-based overload: evaluate in a child scope so the provided TemplateContext is immutable.
            if (isolateContext)
            {
                context.EnterChildScope();
            }

            try
            {
                var initialCapacity = context.Options?.OutputBufferSize ?? 0;
                if (initialCapacity <= 0)
                {
                    initialCapacity = 16 * 1024;
                }

                using var output = new BufferFluidOutput(initialCapacity);
                await template.RenderAsync(output, encoder, context);
                await output.FlushAsync();
                return output.ToString();
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
