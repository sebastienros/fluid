using System.IO;
using System.Threading.Tasks;
using Xunit;
using Fluid.Tool;

namespace Fluid.Tests;

public class LiquidToolTests
{
    private static async Task<(int Code, string Out, string Err)> RunAsync(string stdin, params string[] args)
    {
        var stdout = new StringWriter();
        var stderr = new StringWriter();
        var code = await LiquidTool.RunAsync(args, new StringReader(stdin), stdout, stderr);
        return (code, stdout.ToString(), stderr.ToString());
    }

    [Fact]
    public async Task RendersInlineTemplateWithJsonFromStdin()
    {
        var (code, output, _) = await RunAsync("""{"name":"World","items":[1,2,3],"o":{"a":"b"}}""",
            "-e", "Hello {{ name }} {{ items | size }} {{ o.a }}");

        Assert.Equal(0, code);
        Assert.Equal("Hello World 3 b", output);
    }

    [Fact]
    public async Task NonObjectRootIsExposedAsData()
    {
        var (_, output, _) = await RunAsync("[1,2]", "-e", "{{ data | join: ',' }}");
        Assert.Equal("1,2", output);
    }

    [Fact]
    public async Task SetOverridesJson()
    {
        var (_, output, _) = await RunAsync("""{"a":"json"}""", "-e", "{{ a }}-{{ b }}", "--set", "a=cli", "--set", "b=x=y");
        Assert.Equal("cli-x=y", output);
    }

    [Fact]
    public async Task JsonFilterWorks()
    {
        var (_, output, _) = await RunAsync("""{"d":{"k":[1,"a",null]}}""", "-e", "{{ d | json }}");
        Assert.Equal("""{"k":[1,"a",null]}""", output);
    }

    [Fact]
    public async Task RendersFileWithIncludesToOutputFile()
    {
        var dir = Directory.CreateTempSubdirectory().FullName;
        try
        {
            await File.WriteAllTextAsync(Path.Combine(dir, "part.liquid"), "[{{ n }}]");
            await File.WriteAllTextAsync(Path.Combine(dir, "t.liquid"), "{% render 'part', n: n %}");
            var outFile = Path.Combine(dir, "out.txt");

            var (code, _, err) = await RunAsync("""{"n":5}""", Path.Combine(dir, "t.liquid"), "-o", outFile);

            Assert.Equal(0, code);
            Assert.Equal("", err);
            Assert.Equal("[5]", await File.ReadAllTextAsync(outFile));
        }
        finally
        {
            Directory.Delete(dir, true);
        }
    }

    [Fact]
    public async Task ParseErrorReturnsOneAndWritesStderr()
    {
        var (code, output, err) = await RunAsync("", "-e", "{% if %}");
        Assert.Equal(1, code);
        Assert.Equal("", output);
        Assert.StartsWith("error:", err);
    }

    [Fact]
    public async Task InvalidJsonReturnsOne()
    {
        var (code, _, err) = await RunAsync("{nope", "-e", "x");
        Assert.Equal(1, code);
        Assert.Contains("JSON", err);
    }

    [Fact]
    public async Task StrictFailsOnUndefinedVariable()
    {
        var (code, _, err) = await RunAsync("", "-e", "{{ x }}", "--strict");
        Assert.Equal(1, code);
        Assert.Contains("x", err);
    }

    [Fact]
    public async Task StrictFailsOnUndefinedFilter()
    {
        var (code, _, err) = await RunAsync("", "-e", "{{ 'value' | unknown_filter }}", "--strict");
        Assert.Equal(1, code);
        Assert.Contains("unknown_filter", err);
    }

    [Theory]
    [InlineData("--culture", "fr-FR", "{{ 1.5 }}", "1,5")]
    [InlineData("--timezone", "UTC", "{{ '2024-01-01T12:00:00Z' | date: '%H:%M' }}", "12:00")]
    public async Task AppliesRenderingOptions(string option, string value, string template, string expected)
    {
        var (code, output, err) = await RunAsync("", "-e", template, option, value);
        Assert.Equal(0, code);
        Assert.Equal(expected, output);
        Assert.Equal("", err);
    }

    [Fact]
    public async Task MaxStepsLimitsRendering()
    {
        var (code, _, err) = await RunAsync("", "-e", "{% for i in (1..10) %}{{ i }}{% endfor %}", "--max-steps", "1");
        Assert.Equal(1, code);
        Assert.StartsWith("error:", err);
    }

    [Fact]
    public async Task InvalidTimeZoneIsUsageError()
    {
        var (code, output, err) = await RunAsync("", "-e", "value", "--timezone", "Invalid/TimeZone");
        Assert.Equal(2, code);
        Assert.Equal("", output);
        Assert.StartsWith("error:", err);
    }

    [Fact]
    public async Task ValidateDoesNotRender()
    {
        var (code, output, _) = await RunAsync("", "-e", "{{ x }}", "--validate");
        Assert.Equal(0, code);
        Assert.Equal("", output);
    }

    [Fact]
    public async Task MissingTemplateIsUsageError()
    {
        var (code, _, err) = await RunAsync("");
        Assert.Equal(2, code);
        Assert.Contains("template source", err);
    }

    [Fact]
    public async Task HelpIsPrinted()
    {
        var (code, output, _) = await RunAsync("", "--help");
        Assert.Equal(0, code);
        Assert.Contains("--inline", output);
    }
}
