using System.Runtime.CompilerServices;
using System.Text.Encodings.Web;

namespace Fluid
{
    public static partial class FluidTemplateExtensions
    {
        /// <summary>
        /// Renders the template to a string.
        /// </summary>
        /// <param name="template">The template to render.</param>
        /// <returns>The rendered template as a string.</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static string Render(this IFluidTemplate template)
        {
            return Render(template, new TemplateContext());
        }

        /// <summary>
        /// Renders the template to a string using the specified context.
        /// </summary>
        /// <param name="template">The template to render.</param>
        /// <param name="context">The context to use for rendering.</param>
        /// <returns>The rendered template as a string.</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static string Render(this IFluidTemplate template, TemplateContext context)
        {
            return Render(template, context, NullEncoder.Default);
        }

        /// <summary>
        /// Renders the template to the specified text writer using the specified context and text encoder.
        /// </summary>
        /// <param name="template">The template to render.</param>
        /// <param name="context">The context to use for rendering.</param>
        /// <param name="encoder">The text encoder to use for rendering.</param>
        /// <param name="isolateContext">A boolean value indicating whether to isolate the context during rendering.</param>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static string Render(this IFluidTemplate template, TemplateContext context, TextEncoder encoder, bool isolateContext = true)
        {
            var task = RenderAsync(template, context, encoder, isolateContext);
            return task.IsCompletedSuccessfully ? task.Result : task.AsTask().GetAwaiter().GetResult();
        }

        /// <summary>
        /// Renders the template to the specified text writer using the specified context and text encoder.
        /// This method is obsolete and will be removed in a future version.
        /// </summary>
        /// <param name="template">The template to render.</param>
        /// <param name="context">The context to use for rendering.</param>
        /// <param name="encoder">The text encoder to use for rendering.</param>
        /// <param name="writer">The text writer to write the rendered template to.</param>
        [Obsolete("Use Render(this IFluidTemplate template, TextWriter writer, TemplateContext context, TextEncoder encoder) instead. This method will be removed in a future version.")]
        public static void Render(this IFluidTemplate template, TemplateContext context, TextEncoder encoder, TextWriter writer)
        {
            var task = RenderAsync(template, writer, context, encoder);

            if (!task.IsCompletedSuccessfully)
            {
                task.AsTask().GetAwaiter().GetResult();
            }
        }
    }
}
