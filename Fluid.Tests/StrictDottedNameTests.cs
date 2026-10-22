using System.Threading.Tasks;
using Fluid.Values;
using Xunit;

namespace Fluid.Tests;

/// <summary>
/// A name with dots, such as <c>p['a.b']</c>, is resolved member by member.
/// </summary>
public class StrictDottedNameTests
{
#if COMPILED
    private static readonly FluidParser _parser = new FluidParser().Compile();
#else
    private static readonly FluidParser _parser = new FluidParser();
#endif

    public sealed class Person
    {
        public string Name { get; set; } = "Bill";

        public Person Friend { get; set; }
    }

    private static TemplateContext CreateContext(TemplateOptions options)
    {
        var context = new TemplateContext(options);
        context.SetValue("p", new Person { Friend = new Person { Name = "Ann" } });
        return context;
    }

    [Theory]
    [InlineData("{{ p['a.b'] }}", "Undefined variable 'a'")]
    [InlineData("{{ p['Friend.missing'] }}", "Undefined variable 'Friend.missing'")]
    [InlineData("{{ p['Friend.Name.missing'] }}", "Undefined variable 'Friend.Name.missing'")]
    public async Task StrictVariablesReportThePath(string source, string message)
    {
        var options = new TemplateOptionsBuilder().WithStrictVariables().Build();

        var exception = await Assert.ThrowsAsync<FluidException>(() => _parser.Parse(source).RenderAsync(CreateContext(options)).AsTask());

        Assert.Equal(message, exception.Message);
    }

    [Fact]
    public async Task StrictVariablesResolveExistingPaths()
    {
        var options = new TemplateOptionsBuilder().WithStrictVariables().Build();

        Assert.Equal("Ann", await _parser.Parse("{{ p['Friend.Name'] }}").RenderAsync(CreateContext(options)));
    }

    [Fact]
    public async Task UndefinedReceivesThePath()
    {
        string path = null;
        var options = new TemplateOptionsBuilder()
            .WithUndefined((name, type) =>
            {
                path = name;
                return new ValueTask<FluidValue>(StringValue.Create("?"));
            })
            .Build();

        Assert.Equal("?", await _parser.Parse("{{ p['Friend.missing'] }}").RenderAsync(CreateContext(options)));
        Assert.Equal("Friend.missing", path);
    }

    [Fact]
    public async Task WithoutStrictVariablesNothingIsWritten()
    {
        Assert.Equal("[]", await _parser.Parse("[{{ p['a.b'] }}]").RenderAsync(CreateContext(TemplateOptions.Default)));
    }
}
