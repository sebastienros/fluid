using Fluid.Utils;
using System.IO;
using System.Runtime.CompilerServices;
using System.Text.Encodings.Web;
using System.Threading.Tasks;

namespace Fluid
{
    public static class FluidTemplateExtensions
    {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static ValueTask<string> RenderAsync(this IFluidTemplate template, TemplateContext context)
        {
            return template.RenderAsync(context, NullEncoder.Default);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static string Render(this IFluidTemplate template, TemplateContext context, TextEncoder encoder)
        {
            var task = template.RenderAsync(context, encoder);
            return task.IsCompletedSuccessfully ? task.Result : task.AsTask().GetAwaiter().GetResult();
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static void Render(this IFluidTemplate template, TemplateContext context, TextEncoder encoder, TextWriter writer)
        {
            var task = template.RenderAsync(writer, encoder, context);
            if (!task.IsCompletedSuccessfully)
            {
                task.AsTask().GetAwaiter().GetResult();
            }
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static async ValueTask<string> RenderAsync(this IFluidTemplate template, TemplateContext context, TextEncoder encoder)
        {
            if (context == null)
            {
                ExceptionHelper.ThrowArgumentNullException(nameof(context));
            }

            if (template == null)
            {
                ExceptionHelper.ThrowArgumentNullException(nameof(template));
            }

            var sb = StringBuilderPool.GetInstance();
            var writer = new StringWriter(sb.Builder);

            // A template is evaluated in a child scope such that the provided TemplateContext is immutable
            context.EnterChildScope();

            try
            {
                await template.RenderAsync(writer, encoder, context);

                writer.Flush();
                return sb.ToString();
            }
            finally
            {
                sb.Dispose();
                writer.Dispose();
                context.ReleaseScope();
            }
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static string Render(this IFluidTemplate template, TemplateContext context)
        {
            var task = template.RenderAsync(context);
            return task.IsCompletedSuccessfully ? task.Result : task.AsTask().GetAwaiter().GetResult();
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static ValueTask<string> RenderAsync(this IFluidTemplate template)
        {
            return template.RenderAsync(new TemplateContext());
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static string Render(this IFluidTemplate template)
        {
            var task = template.RenderAsync();
            return task.IsCompletedSuccessfully ? task.Result : task.AsTask().GetAwaiter().GetResult();
        }
    }
}
