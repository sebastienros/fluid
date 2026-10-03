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
    // Enable all parsing options to ensure these custom features don't interfere with standard templates.

#if COMPILED
        private static FluidParser _parser = new FluidParser(new FluidParserOptions { AllowFunctions = true, AllowParentheses = true }).Compile();
#else
    private static FluidParser _parser = new FluidParser(new FluidParserOptions { AllowFunctions = true, AllowParentheses = true });
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
        Assert.IsType<UndefinedValue>(context.GetValue("hello"));
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
    public async Task  FromStatement_ShouldInvokeImportedMacros()
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

    [Fact]
    public async Task FromStatement_WithoutImportList_ShouldOnlyImportAllMacros()
    {
        var fileProvider = new MockFileProvider();
        fileProvider.Add("_Macros.liquid", @"
        {%- assign imported_value = 'should not escape' -%}
        SHOULD_NOT_RENDER
        {%- macro hello_world() -%}
        Hello world!
        {%- endmacro -%}
        {%- macro hello(name) -%}
        Hello {{ name }}!
        {%- endmacro -%}
        ");

        var source = "{% from '_Macros' %}{{ hello_world() }} {{ hello('John') }}|{{ imported_value }}";
        _parser.TryParse(source, out var template, out var error);
        Assert.True(template != null, error);

        var options = new TemplateOptions { FileProvider = fileProvider };
        var result = await template.RenderAsync(new TemplateContext(options));

        Assert.Equal("Hello world! Hello John!|", result);
    }

    [Fact]
    public async Task FromStatement_ShouldLoadMacrosAsynchronously()
    {
        var sourceLoader = new AsyncTemplateFileProvider()
            .Add("_Macros.liquid", "{% macro hello() %}Hello{% endmacro %}");
        var options = new TemplateOptions { FileProvider = sourceLoader };
        var source = "{% from '_Macros' import hello %}{{ hello() }}";

        _parser.TryParse(source, out var template, out var error);

        var renderTask = template.RenderAsync(new TemplateContext(options));
        Assert.False(renderTask.IsCompletedSuccessfully);
        Assert.Equal("Hello", await renderTask);
        Assert.Equal(1, sourceLoader.GetReadCount("_Macros.liquid"));
    }

    [Fact]
    public async Task FromStatement_ShouldReloadMacrosWhenSourceVersionChanges()
    {
        var sourceLoader = new AsyncTemplateFileProvider()
            .Add("_Macros.liquid", "{% macro hello() %}First{% endmacro %}");
        var options = new TemplateOptions { FileProvider = sourceLoader };
        _parser.TryParse("{% from '_Macros' import hello %}{{ hello() }}", out var template);

        Assert.Equal("First", await template.RenderAsync(new TemplateContext(options)));

        sourceLoader.Add("_Macros.liquid", "{% macro hello() %}Second{% endmacro %}");

        Assert.Equal("Second", await template.RenderAsync(new TemplateContext(options)));
        Assert.Equal(2, sourceLoader.GetReadCount("_Macros.liquid"));
    }
}
