﻿using System;
using System.IO;
using System.Text.Encodings.Web;
using System.Threading.Tasks;

namespace Fluid
{
    public static class FluidTemplateExtensions
    {
        public static ValueTask<string> RenderAsync(this IFluidTemplate template, TemplateContext context)
        {
            return template.RenderAsync(context, NullEncoder.Default);
        }

        public static string Render(this IFluidTemplate template, TemplateContext context, TextEncoder encoder)
        {
            return template.RenderAsync(context, encoder).GetAwaiter().GetResult();
        }

        public static void Render(this IFluidTemplate template, TemplateContext context, TextEncoder encoder, TextWriter writer)
        {
            template.RenderAsync(writer, encoder, context).GetAwaiter().GetResult();
        }

        public static async ValueTask<string> RenderAsync(this IFluidTemplate template, TemplateContext context, TextEncoder encoder)
        {
            if (context == null)
            {
                throw new ArgumentNullException(nameof(context));
            }

            if (template == null)
            {
                throw new ArgumentNullException(nameof(template));
            }

            using (var sb = StringBuilderPool.GetInstance())
            {
                using (var writer = new StringWriter(sb.Builder))
                {
                    await template.RenderAsync(writer, encoder, context);
                    await writer.FlushAsync();
                }

                return sb.ToString();
            }
        }

        public static string Render(this IFluidTemplate template, TemplateContext context)
        {
            return template.RenderAsync(context).GetAwaiter().GetResult();
        }

        public static ValueTask<string> RenderAsync(this IFluidTemplate template)
        {
            return template.RenderAsync(new TemplateContext());
        }

        public static string Render(this IFluidTemplate template)
        {
            return template.RenderAsync().GetAwaiter().GetResult();
        }
    }
}
