using System.Runtime.CompilerServices;
using System.Text.Encodings.Web;

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
            if (textWriter == null)
            {
                ExceptionHelper.ThrowArgumentNullException(nameof(textWriter));
            }

            if (context == null)
            {
                ExceptionHelper.ThrowArgumentNullException(nameof(context));
            }

            if (template == null)
            {
                ExceptionHelper.ThrowArgumentNullException(nameof(template));
            }

            // A template is evaluated in a child scope such that the provided TemplateContext is immutable
            if (isolateContext)
            {
                context.EnterChildScope();
            }

            try
            {
                await template.RenderAsync(textWriter, encoder, context);

                textWriter.Flush();
            }
            finally
            {
                textWriter.Dispose();

                if (isolateContext)
                {
                    context.ReleaseScope();
                }
            }
        }
    }
}
