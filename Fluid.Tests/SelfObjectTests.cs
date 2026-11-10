using System.Collections.Generic;
using System.Threading.Tasks;
using Fluid.Tests.Mocks;
using Xunit;

namespace Fluid.Tests;

public class SelfObjectTests
{
    private static readonly FluidParser _parser = new FluidParser();

    private static readonly MockFileProvider _fileProvider = new MockFileProvider()
        .Add("p.liquid", "[{{ v }}]")
        .Add("selfp.liquid", "{{ self.x }}|{{ self.v }}|{{ self.s }}");

    private static TemplateOptions Options { get; } = new TemplateOptionsBuilder().WithFileProvider(_fileProvider).Build();

    private static async Task<string> RenderAsync(string source, TemplateContext context)
    {
        return await _parser.Parse(source).RenderAsync(context);
    }

    private static TemplateContext CreateContext()
    {
        var context = new TemplateContext(Options);
        context.SetValue("s", "Hello World");
        context.SetValue("h", new Dictionary<string, object> { ["k"] = "v" });
        context.SetValue("a", new[] { 1, 2, 3 });
        return context;
    }

    [Theory]
    // Like Liquid's SelfDrop, the members of self are the variables, in every scope.
    [InlineData("[{{ self.s }}]|[{{ self['s'] }}]|{% assign x = 1 %}[{{ self.x }}]|{% assign k = 's' %}[{{ self[k] }}]|[{{ [k] }}]", "[Hello World]|[Hello World]|[1]|[Hello World]|[Hello World]")]
    [InlineData("{% for i in (1..2) %}[{{ self.i }}]{% endfor %}|{% capture c %}x{% endcapture %}[{{ self.c }}]|{% increment n %}[{{ self.n }}]", "[1][2]|[x]|0[1]")]
    [InlineData("{% if self.s %}T{% endif %}|{% if self %}S{% endif %}|{{ self.h.k }}|{{ self.a[0] }}|{% render 'p', v: self.s %}", "T|S|v|1|[Hello World]")]
    [InlineData("{% for i in (1..2) %}{{ self.forloop.index }}{% endfor %}|{% decrement d %}[{{ self.d }}]", "12|-1[-1]")]
    [InlineData("{% assign v = self %}{{ v.s }}|{% assign z = 3 %}{{ v.z }}|{% if v == self %}E{% endif %}|{{ self.s | upcase }}", "Hello World|3|E|HELLO WORLD")]
    // Its own members are variables too: self has no size or first, and only strings name variables.
    [InlineData("[{{ self }}]|[{{ self | size }}]|[{{ self.size }}]|[{{ self.first }}]|[{{ self[1] }}]|[{{ self[nil] }}]|[{{ self.missing }}]", "[Liquid::SelfDrop]|[0]|[]|[]|[]|[]|[]")]
    // Like Liquid, the self member of self and a bare bracket are variable lookups too: the self object, unless a variable is named self.
    [InlineData("{% assign y = 2 %}[{{ self.self }}][{{ self.self.y }}][{{ self['self'].y }}]|{% assign k = 'self' %}[{{ [k] }}][{{ [k].y }}][{{ ['self']['y'] }}]|{% if self.self == self %}E{% endif %}{% assign v = self %}{% if v.self == v %}V{% endif %}", "[Liquid::SelfDrop][2][2]|[Liquid::SelfDrop][2][2]|EV")]
    [InlineData("{% assign y = 2 %}{% assign self = nil %}[{{ self.self }}][{{ ['self'] }}][{{ ['self'].y }}]|{% assign self = 'v' %}[{{ ['self'] }}]", "[][][]|[v]")]
    [InlineData("{% if self == self %}1{% endif %}{% if self != self %}2{% endif %}{% if self == empty %}3{% endif %}{% if self == blank %}4{% endif %}{% if self == nil %}5{% endif %}{% if self contains 's' %}6{% endif %}{% if self == s %}7{% endif %}", "1")]
    [InlineData("{% for v in self %}[{{ v }}]{% endfor %}|{{ self | default: 'd' }}|{% case self %}{% when self %}W{% else %}X{% endcase %}|{{ 'x' | append: self }}", "|Liquid::SelfDrop|W|xLiquid::SelfDrop")]
    // A variable named self, even nil, takes precedence.
    [InlineData("{% assign self = 'v' %}[{{ self }}]|{{ self.size }}", "[v]|1")]
    [InlineData("{% assign self = nil %}[{{ self.s }}]{% if self %}Y{% endif %}", "[]")]
    [InlineData("{% assign x = 'X' %}{% for self in (1..2) %}{{ self }}{% endfor %}|{{ self.x }}|{% capture self %}cap{% endcapture %}{{ self }}", "12|X|cap")]
    // In a render partial, self only sees the partial's arguments and the values of the context, like its variables.
    [InlineData("{% assign x = 1 %}[{% render 'selfp', v: 2 %}]|[{% include 'selfp', v: 2 %}]", "[|2|Hello World]|[1|2|Hello World]")]
    [InlineData("{% assign self = 'q' %}{% render 'selfp', v: 2 %}", "|2|Hello World")]
    public async Task SelfResolvesVariablesLikeLiquid(string source, string expected)
    {
        Assert.Equal(expected, await RenderAsync(source, CreateContext()));
    }

    [Theory]
    // Like Liquid (liquid-spec bare_bracket_self.yml), a self object passed to a partial keeps reading the variables
    // of the template it comes from, as they are while it renders the partial.
    [InlineData("{%- assign var = 42 -%}{%- assign s = self -%}{%- render 'snippet1', other_self: s -%}", "{%- assign var = 43 -%}{{- other_self.var }}|{{ self.var -}}", null, "42|43")]
    [InlineData("{%- assign x = 1 -%}{%- render 'snippet1', self: self -%}", "{%- assign x = 2 -%}{{- self.x }}|{%- assign self = nil -%}{{ self.x -}}", null, "1|")]
    [InlineData("{%- assign x = 42 -%}{%- assign s = self -%}{%- render 'snippet1', drop: s -%}", "{{- drop.x -}}|{% if drop == self %}same{% else %}other{% endif %}", null, "42|other")]
    [InlineData("{%- assign x = 1 -%}{%- assign s1 = self -%}{{- s1.x -}}|{%- render 'snippet1', outer: s1 -%}", "{%- assign x = 2 -%}{%- assign s2 = self -%}{{- outer.x }}/{{ s2.x -}}|{%- render 'snippet2', outer: outer, middle: s2 -%}", "{%- assign x = 3 -%}{{- outer.x }}/{{ middle.x }}/{{ self.x -}}", "1|1/2|1/2/3")]
    [InlineData("{% for i in (1..2) %}{% render 'snippet1', outer: self %}{% endfor %}", "{{ outer.i }}{{ i }}", null, "12")]
    [InlineData("{% assign s = self %}{% render 'snippet1' for (1..2) as v, o: s %}", "{% assign q = v %}{% if o == self %}E{% else %}N{% endif %}{{ o.q }}", null, "NN")]
    [InlineData("{% increment c %}{% assign s = self %}{% render 'snippet1', o: s %}", "[{{ o.c }}][{{ c }}]{% increment c %}[{{ o.c }}][{{ self.c }}]", null, "0[1][]0[1][1]")]
    // The scopes of loops and of include are in the same template.
    [InlineData("{% assign s = self %}{% for i in (1..2) %}{% if s == self %}E{% endif %}{% endfor %}{% include 'snippet1' %}", "{% if s == self %}I{% endif %}", null, "EEI")]
    public async Task SelfPassedToAPartialReadsItsTemplate(string source, string snippet1, string snippet2, string expected)
    {
        var fileProvider = new MockFileProvider().Add("snippet1.liquid", snippet1);

        if (snippet2 != null)
        {
            fileProvider.Add("snippet2.liquid", snippet2);
        }

        var context = new TemplateContext(new TemplateOptionsBuilder().WithFileProvider(fileProvider).Build());

        Assert.Equal(expected, await RenderAsync(source, context));
    }

    [Fact]
    public async Task SelfReadsTheModel()
    {
        var context = new TemplateContext(new { Title = "T", Items = new[] { "a", "b" } }, Options);

        Assert.Equal("T|b|T", await RenderAsync("{{ self.Title }}|{{ self.Items[1] }}|{{ self['Title'] }}", context));
    }

    [Fact]
    public async Task ModelMemberNamedSelfTakesPrecedence()
    {
        var context = new TemplateContext(new Dictionary<string, object> { ["self"] = "model", ["x"] = 1 }, Options);

        Assert.Equal("model||model", await RenderAsync("{{ self }}|{{ self.x }}|{{ ['self'] }}", context));
    }

    [Fact]
    public async Task SelfMemberOfSelfReadsTheModel()
    {
        var context = new TemplateContext(new Dictionary<string, object> { ["x"] = 1 }, Options);

        Assert.Equal("Liquid::SelfDrop|1|1", await RenderAsync("{{ self.self }}|{{ self.self.x }}|{{ ['self'].x }}", context));
    }

    [Fact]
    public async Task SelfIsNotAnUndefinedVariable()
    {
        var context = new TemplateContext(new TemplateOptionsBuilder().WithStrictVariables(true).Build());
        context.SetValue("x", 1);

        Assert.Equal("Liquid::SelfDrop|1", await RenderAsync("{{ self }}|{{ self.x }}", context));
        await Assert.ThrowsAsync<FluidException>(() => RenderAsync("{{ self.missing }}", context));
    }
}
