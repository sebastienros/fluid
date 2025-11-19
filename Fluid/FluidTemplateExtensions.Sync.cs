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

    }
}
