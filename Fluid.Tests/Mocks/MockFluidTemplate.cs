using System.IO;
using System.Text.Encodings.Web;
using System.Threading.Tasks;

namespace Fluid.Tests.Mocks;

public class MockFluidTemplate : IFluidTemplate
{
    private readonly string _content;

    public MockFluidTemplate(string content)
    {
        _content = content;
    }

    public ValueTask RenderAsync(IFluidOutput output, TextEncoder encoder, TemplateContext context)
    {
        output.Write(_content);

        return ValueTask.CompletedTask;
    }
}
