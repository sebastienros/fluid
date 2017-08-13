using System;
using System.Collections.Generic;
using System.IO;
using System.Text.Encodings.Web;
using System.Threading.Tasks;
using Fluid.Ast;

namespace Fluid
{
    public interface IFluidTemplate
    {
        IList<Statement> Statements { get; set; }
        Task RenderAsync(TextWriter writer, TextEncoder encoder, TemplateContext context);
    }

    public static class FluidTemplateExtensions
    {
        public static void Render(this IFluidTemplate template, TextWriter writer, TextEncoder encoder, TemplateContext context)
        {
            template.RenderAsync(writer, encoder, context).GetAwaiter().GetResult();
        }

        public static async Task<string> RenderAsync(this IFluidTemplate template, TemplateContext context)
        {
            if (context == null)
            {
                throw new ArgumentNullException(nameof(context));
            }

            if (template == null)
            {
                throw new ArgumentNullException(nameof(template));
            }

            using (var writer = new StringWriter())
            {
                await template.RenderAsync(writer, HtmlEncoder.Default, context);
                return writer.ToString();
            }
        }

        public static string Render(this IFluidTemplate template, TemplateContext context)
        {
            return template.RenderAsync(context).GetAwaiter().GetResult();
        }

        public static Task<string> RenderAsync(this IFluidTemplate template)
        {
            return template.RenderAsync(new TemplateContext());
        }

        public static string Render(this IFluidTemplate template)
        {
            return template.RenderAsync().GetAwaiter().GetResult();
        }
    }

    public class FluidTemplate : BaseFluidTemplate<FluidTemplate>
    {
    }
}
