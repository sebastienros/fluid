using System.Collections.Generic;
using System.IO;
using System.Text.Encodings.Web;
using System.Threading.Tasks;
using Fluid.Ast;

namespace Fluid
{
    public class BaseFluidTemplate<T> : IFluidTemplate where T : IFluidTemplate, new()
    {
        static BaseFluidTemplate()
        {
            // Necessary to force the custom template class static constructor
            // c.f. https://github.com/sebastienros/fluid/issues/19
            new T();
        }

        public static FluidParserFactory Factory { get; } = new FluidParserFactory();

        public IList<Statement> Statements { get; set; }

        public BaseFluidTemplate()
        {
            Statements = new List<Statement>();
        }

        public BaseFluidTemplate(IList<Statement> statements)
        {
            Statements = statements;
        }

        public static bool TryParse(string template, out T result, out IEnumerable<string> errors)
        {
            if (Factory.CreateParser().TryParse(template, out var statements, out errors))
            {
                result = new T();
                result.Statements = statements;
                return true;
            }
            else
            {
                result = default(T);
                return false;
            }
        }

        public static bool TryParse(string template, out T result)
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
}
