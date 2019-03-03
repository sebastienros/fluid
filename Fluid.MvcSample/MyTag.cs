using Fluid.Ast;
using Fluid.Tags;
using System.IO;
using System.Text.Encodings.Web;
using System.Threading.Tasks;

namespace Fluid.MvcSample
{
    public class MyTag : SimpleTag
    {
        public override async ValueTask<Completion> WriteToAsync(TextWriter writer, TextEncoder encoder, TemplateContext context)
        {
            await writer.WriteAsync("Hello from MyTag");

            return Completion.Normal;
        }
    }
}
