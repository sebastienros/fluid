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
        IList<Statement> Statements { get; }
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

    public class BaseFluidTemplate<T> : IFluidTemplate
    {
        public static FluidParserFactory Factory { get; } = new FluidParserFactory();

        public IList<Statement> Statements { get; }

        public BaseFluidTemplate()
        {
            Statements = new List<Statement>();
        }

        public BaseFluidTemplate(IList<Statement> statements)
        {
            Statements = statements;
        }

        public static bool TryParse(string template, out IFluidTemplate result, out IEnumerable<string> errors)
        {
            if (Factory.CreateParser().TryParse(template, out var statements, out errors))
            {
                result = new BaseFluidTemplate<T>(statements);
                return true;
            }
            else
            {
                result = null;
                return false;
            }
        }

        public static bool TryParse(string template, out IFluidTemplate result)
        {
            return TryParse(template, out result, out var errors);
        }

        public async Task RenderAsync(TextWriter writer, TextEncoder encoder, TemplateContext context)
        {
            foreach (var statement in Statements)
            {
                await statement.WriteToAsync(writer, encoder, context);
            }
        }
    }

    public class FluidTemplate : BaseFluidTemplate<FluidTemplate>
    {
    }
}
