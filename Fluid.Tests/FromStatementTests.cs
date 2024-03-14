using System.Collections.Generic;
using System.IO;
using System.Text.Encodings.Web;
using System.Threading.Tasks;
using Fluid.Ast;
using Fluid.Tests.Mocks;
using Fluid.Values;
using Xunit;

namespace Fluid.Tests;

public class FromStatementTests
{
#if COMPILED
        private static FluidParser _parser = new FluidParser(new FluidParserOptions { AllowFunctions = true }).Compile();
#else
    private static FluidParser _parser = new FluidParser(new FluidParserOptions { AllowFunctions = true });
#endif

    [Fact]
    public async Task FromStatement_ShouldThrowFileNotFoundException_IfTheFileProviderIsNotPresent()
    {
        var expression = new LiteralExpression(new StringValue("_Macros.liquid"));
        var sw = new StringWriter();

        try
        {
            var fromStatement = new FromStatement(_parser, expression, new List<string> { "foo" });
            await fromStatement.WriteToAsync(sw, HtmlEncoder.Default, new TemplateContext());

            Assert.True(false);
        }
        catch (FileNotFoundException)
        {
            return;
        }
    }

    [Fact]
    public async Task FromStatement_ShouldOnlyImportListedMacrosToLocalScope()
    {
        var expression = new LiteralExpression(new StringValue("_Macros.liquid"));
        var sw = new StringWriter();

        var fileProvider = new MockFileProvider();
        fileProvider.Add("_Macros.liquid", @"
        {% macro hello_world() %}
        Hello world!
        {% endmacro %}

        {% macro hello(first, last='Smith') %}
        Hello {{first | capitalize}} {{last}}!
        {% endmacro %}
        ");

        var options = new TemplateOptions { FileProvider = fileProvider };
        var context = new TemplateContext(options);

        var fromStatement = new FromStatement(_parser, expression, new List<string>{"hello_world"});
        await fromStatement.WriteToAsync(sw, HtmlEncoder.Default, context);

        Assert.IsType<FunctionValue>(context.GetValue("hello_world"));
        Assert.IsType<NilValue>(context.GetValue("hello"));
    }

    [Fact]
    public async Task FromStatement_ShouldNotRenderAnyOutput()
    {
        var expression = new LiteralExpression(new StringValue("_Macros.liquid"));
        var sw = new StringWriter();

        var fileProvider = new MockFileProvider();
        fileProvider.Add("_Macros.liquid", @"
        {% macro hello_world() %}
        Hello world!
        {% endmacro %}

        {% macro hello(first, last='Smith') %}
        Hello {{first | capitalize}} {{last}}!
        {% endmacro %}

        {{ hello_world() }}
        ");

        var options = new TemplateOptions { FileProvider = fileProvider };
        var context = new TemplateContext(options);

        var fromStatement = new FromStatement(_parser, expression, new List<string> { "hello_world" });
        await fromStatement.WriteToAsync(sw, HtmlEncoder.Default, context);

        var result = sw.ToString();
        Assert.Equal("", result);
    }

    [Fact]
    public async Task  FromStatement_ShouldInvokeImportedMarcos()
    {
        var expression = new LiteralExpression(new StringValue("_Macros.liquid"));
        var sw = new StringWriter();

        var fileProvider = new MockFileProvider();
        fileProvider.Add("_Macros.liquid", @"
        {%- macro hello_world() -%}
        Hello world!
        {%- endmacro -%}

        {%- macro hello(first, last='Doe') -%}
        Hello {{first | capitalize}} {{last}}!
        {%- endmacro -%}
        ");

        var source = @"
        {%- from '_Macros' import hello_world, hello -%}
        {{ hello_world() }} {{ hello('John') }}";


        _parser.TryParse(source, out var template, out var error);

        var options = new TemplateOptions { FileProvider = fileProvider };
        var context = new TemplateContext(options);

        var result = await template.RenderAsync(context);
        Assert.Equal("Hello world! Hello John Doe!", result);
    }
}