using System.Collections.Generic;
using System.IO;
using System.Runtime.CompilerServices;
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
            // as the only member accessed is defined on this class
            RuntimeHelpers.RunClassConstructor(typeof(T).TypeHandle);
        }

        public static FluidParserFactory Factory { get; } = new FluidParserFactory();

        public List<Statement> Statements { get; set; } = new List<Statement>();

        public static bool TryParse(string template, out T result, out IEnumerable<string> errors)
        {
            return TryParse(template, true, out result, out errors);
        }

        public static bool TryParse(string template, bool stipEmptyLines, out T result, out IEnumerable<string> errors)
        {
            if (Factory.CreateParser().TryParse(template, stipEmptyLines, out var statements, out errors))
            {
                result = new T
                {
                    Statements = statements
                };
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
