using System.Linq;
using System.Threading.Tasks;
using Fluid.Playground;
using Xunit;

namespace Fluid.Tests.Playground;

public class PlaygroundTests
{
    private readonly PlaygroundEngine _engine = new();

    public static TheoryData<string> SampleIds() => new(PlaygroundSamples.All.Select(s => s.Id));

    [Theory]
    [MemberData(nameof(SampleIds))]
    public async Task SamplesRender(string id)
    {
        var sample = PlaygroundSamples.All.Single(s => s.Id == id);

        var result = await _engine.RenderAsync(sample.State);

        if (id == "strict")
        {
            // The sample shows the error of an undefined variable.
            Assert.Equal(PlaygroundErrorKind.Render, result.Error?.Kind);
            Assert.Contains("name", result.Error.Message);
        }
        else
        {
            Assert.Null(result.Error);
            Assert.NotEmpty(result.Output.Trim());
        }
    }

    [Fact]
    public void StateRoundTripsThroughTheHash()
    {
        var state = new PlaygroundState
        {
            Template = "{% render 'a' %} é 😀",
            Model = """{ "x": 1 }""",
            Partials = [new("a", "{{ x }}")],
            Options = new PlaygroundOptions { AllowFunctions = true, Greedy = false, Culture = "fr-FR", Trimming = TrimmingFlags.TagLeft | TrimmingFlags.OutputRight, MaxSteps = 10 },
        };

        var hash = state.ToHash();
        var read = PlaygroundState.FromHash("#" + hash);

        Assert.DoesNotContain('+', hash);
        Assert.DoesNotContain('/', hash);
        Assert.DoesNotContain('=', hash);
        Assert.NotNull(read);
        Assert.Equal(state.Template, read.Template);
        Assert.Equal(state.Model, read.Model);
        Assert.Equal(state.Partials, read.Partials);
        Assert.Equal(state.Options, read.Options);
    }

    [Fact]
    public void DefaultOptionsRoundTrip()
    {
        var read = PlaygroundState.FromHash(new PlaygroundState { Template = "x" }.ToHash());

        Assert.Equal(new PlaygroundOptions(), read?.Options);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("#")]
    [InlineData("#not base64!")]
    [InlineData("#AAAA")]
    public void InvalidHashesAreIgnored(string hash)
    {
        Assert.Null(PlaygroundState.FromHash(hash));
    }

    [Fact]
    public async Task ParseErrorsHaveAPosition()
    {
        var result = await _engine.RenderAsync(new PlaygroundState { Template = "line 1\n{% if %}" });

        Assert.Equal(PlaygroundErrorKind.Parse, result.Error?.Kind);
        Assert.Equal(2, result.Error.Line);
        Assert.NotNull(result.Error.Column);
    }

    [Fact]
    public async Task ModelErrorsHaveAPosition()
    {
        var result = await _engine.RenderAsync(new PlaygroundState { Template = "{{ a }}", Model = "{\n  \"a\": }" });

        Assert.Equal(PlaygroundErrorKind.Model, result.Error?.Kind);
        Assert.Equal(2, result.Error.Line);
        Assert.DoesNotContain("Path:", result.Error.Message);
    }

    [Fact]
    public async Task ModelAcceptsCommentsAndTrailingCommas()
    {
        var result = await _engine.RenderAsync(new PlaygroundState { Template = "{{ a }}", Model = "{ // the value\n \"a\": 1, }" });

        Assert.Null(result.Error);
        Assert.Equal("1", result.Output);
    }

    [Fact]
    public async Task ModelThatIsNotAnObjectIsData()
    {
        var result = await _engine.RenderAsync(new PlaygroundState { Template = "{{ data | join: '-' }}", Model = "[1, 2, 3]" });

        Assert.Equal("1-2-3", result.Output);
    }

    [Fact]
    public async Task PartialsAreFoundWithOrWithoutTheExtension()
    {
        var result = await _engine.RenderAsync(new PlaygroundState
        {
            Template = "{% render 'a', v: 1 %}|{% include 'b.liquid' %}",
            Partials = [new("a", "A{{ v }}"), new("b", "B")],
        });

        Assert.Null(result.Error);
        Assert.Equal("A1|B", result.Output);
    }

    [Fact]
    public async Task MissingPartialIsAnError()
    {
        var result = await _engine.RenderAsync(new PlaygroundState { Template = "{% render 'missing' %}", Partials = [new("a", "A")] });

        Assert.Equal(PlaygroundErrorKind.Render, result.Error?.Kind);
    }

    [Fact]
    public async Task OptionsApply()
    {
        var state = new PlaygroundState
        {
            Template = "{{ name }} {{ '<b>' }} {{ 1234.5 }} {{ 10 | money }}",
            Model = """{ "name": "Ada" }""",
            Options = new PlaygroundOptions { HtmlEncode = true, Culture = "fr-FR", MoneyFilters = true },
        };

        var result = await _engine.RenderAsync(state);

        Assert.Null(result.Error);
        Assert.Equal("Ada &lt;b&gt; 1234,5 10,00 €", result.Output);
    }

    [Fact]
    public async Task ParserOptionsApply()
    {
        var template = "{{ 1 | plus: (2 | times: 3) }}";

        var without = await _engine.RenderAsync(new PlaygroundState { Template = template });
        var with = await _engine.RenderAsync(new PlaygroundState { Template = template, Options = new PlaygroundOptions { AllowParentheses = true } });

        Assert.Equal(PlaygroundErrorKind.Parse, without.Error?.Kind);
        Assert.Equal("7", with.Output);
    }

    [Fact]
    public async Task StepLimitStopsInfiniteWork()
    {
        var result = await _engine.RenderAsync(new PlaygroundState
        {
            Template = "{% for i in (1..1000) %}{% for j in (1..1000) %}{% endfor %}{% endfor %}",
            Options = new PlaygroundOptions { MaxSteps = 1000 },
        });

        Assert.Equal(PlaygroundErrorKind.Render, result.Error?.Kind);
    }
}
