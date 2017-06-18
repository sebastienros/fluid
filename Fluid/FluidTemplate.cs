using System;
using System.Collections.Generic;
using System.IO;
using System.Text.Encodings.Web;
using System.Threading.Tasks;
using Fluid.Ast;
using Microsoft.Extensions.Primitives;

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

             using(var writer = new StringWriter())
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

    public class FluidTemplate : FluidTemplate<IronyFluidParserFactory>
    {
        public FluidTemplate(IList<Statement> statements) : base(statements)
        {
        }
    }

    public class FluidTemplate<T> : IFluidTemplate where T : IFluidParserFactory, new()
    {
        private static IFluidParserFactory _factory = new T();
        public IList<Statement> Statements { get; }

        public FluidTemplate()
        {
            Statements = new List<Statement>();
        }

        public FluidTemplate(IList<Statement> statements)
        {
            Statements = statements;
        }

        public static bool TryParse(string text, out IFluidTemplate result)
        {
            return TryParse(new StringSegment(text), out result, out var errors);
        }

        public static bool TryParse(string text, out IFluidTemplate result, out IEnumerable<string> errors)
        {
            return TryParse(new StringSegment(text), out result, out errors);
        }

        public static bool TryParse(StringSegment text, out IFluidTemplate result, out IEnumerable<string> errors)
        {
            if (_factory.CreateParser().TryParse(text, out var statements, out errors))
            {
                result =  new FluidTemplate<T>(statements);
                return true;
            }
            else
            {
                result = null;
                return false;
            }            
        }

        public async Task RenderAsync(TextWriter writer, TextEncoder encoder, TemplateContext context)
        {
            foreach(var statement in Statements)
            {
                await statement.WriteToAsync(writer, encoder, context);
            }
        }
    }
}
