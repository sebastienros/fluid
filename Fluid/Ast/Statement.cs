using System.IO;
using System.Text.Encodings.Web;
using System.Threading.Tasks;

namespace Fluid.Ast
{
    public abstract class Statement
    {
        public static ValueTask<Completion> Break() => new(Completion.Break);
        public static ValueTask<Completion> Normal() => new(Completion.Normal);
        public static ValueTask<Completion> Continue() => new(Completion.Continue);

        public abstract ValueTask<Completion> WriteToAsync(TextWriter writer, TextEncoder encoder, TemplateContext context);
    }
}