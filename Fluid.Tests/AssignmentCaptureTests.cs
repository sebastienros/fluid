using System.Linq;
using Fluid.Values;
using Fluid.Filters;
using Xunit;
using System.Threading.Tasks;
using Fluid.Tests.Extensibility;

namespace Fluid.Tests;

public class AssignmentCaptureTests
{
    private static FluidParser _parser = new FluidParser(new FluidParserOptions { AllowFunctions = true });

    [Fact]
    public async Task Assign()
    {
        var source = @"
                {%- assign a = 'b' %}{{a-}}
            ";

        _parser.TryParse(source, out var template, out var error);
        var context = new TemplateContext
        {
            Assigned = (identifier, value, context) => StringValue.Create(value.ToStringValue(context) + "_altered")
        };
        var result = await template.RenderAsync(context);
        Assert.Equal("b_altered", result);
    }

    [Theory]
    [InlineData("{% assign aA1_-.[] = 'hello' %}{{ ['aA1_-.[]'] }}", "hello")]
    [InlineData("{% assign (aA1_-.[])(a)(foo) = 'hello' %}{{ ['(aA1_-.[])(a)(foo)'] }}", "hello")]
    [InlineData("{% assign a.b = 'hello' %}{{ ['a.b'] }}|{{ a.b }}|{{ a }}", "hello||")]
    [InlineData("{% capture x[0] %}hello{% endcapture %}{{ ['x[0]'] }}", "hello")]
    [InlineData("{% increment a.b %}{% increment a.b %}{{ ['a.b'] }}", "012")]
    [InlineData("{% increment x-%} {{ x }}", "01")]
    public void ShouldAcceptShopifyVariableSignatures(string source, string expected)
    {
        Assert.True(_parser.TryParse(source, out var template, out var errors), errors);
        Assert.Equal(expected, template.Render());
    }
}
