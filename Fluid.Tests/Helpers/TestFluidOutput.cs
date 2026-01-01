using Fluid.Ast;
using Fluid.Utils;
using System.IO;
using System.Text.Encodings.Web;
using System.Threading.Tasks;

namespace Fluid.Tests.Helpers;

internal static class TestFluidOutput
{
    public static async Task<(Completion completion, string text)> WriteAsync(Statement statement, TemplateContext context, TextEncoder encoder)
    {
        using var writer = new StringWriter();
        using var output = new TextWriterFluidOutput(writer, bufferSize: 16 * 1024, leaveOpen: true);

        var completion = await statement.WriteToAsync(output, encoder, context);
        await output.FlushAsync();

        return (completion, writer.ToString());
    }
}
