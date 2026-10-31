using System.Collections.Generic;
using System.Threading.Tasks;
using Xunit;

namespace Fluid.Tests;

public class DictionaryOutputTests
{
    [Theory]
    // Like Liquid (Utils.inspect), a hash is written in Ruby's inspect format, in output and in string filters.
    [InlineData("{{ h }}", "{\"k\"=>\"v\", \"n\"=>1, \"f\"=>2.5, \"t\"=>true, \"z\"=>nil, \"sub\"=>{\"x\"=>\"y\"}, \"list\"=>[1, \"a\", nil, []], \"empty\"=>{}}")]
    [InlineData("{{ h.sub | append: '!' }}|{{ e }}|{{ h.sub | size }}", "{\"x\"=>\"y\"}!|{}|1")]
    // Strings are quoted and escaped like Ruby's String#inspect.
    [InlineData("{{ s }}", "{\"q\"=>\"say \\\"hi\\\"\", \"b\"=>\"a\\\\b\", \"n\"=>\"a\\nb\\tc\", \"i\"=>\"\\#{x} #y\", \"c\"=>\"\\u0001é😀\"}")]
    public async Task HashesAreWrittenLikeLiquid(string source, string expected)
    {
        var context = new TemplateContext();
        context.SetValue("h", new Dictionary<string, object>
        {
            ["k"] = "v",
            ["n"] = 1,
            ["f"] = 2.5m,
            ["t"] = true,
            ["z"] = null,
            ["sub"] = new Dictionary<string, object> { ["x"] = "y" },
            ["list"] = new object[] { 1, "a", null, new int[0] },
            ["empty"] = new Dictionary<string, object>(),
        });
        context.SetValue("e", new Dictionary<string, object>());
        context.SetValue("s", new Dictionary<string, object>
        {
            ["q"] = "say \"hi\"",
            ["b"] = "a\\b",
            ["n"] = "a\nb\tc",
            ["i"] = "#{x} #y",
            ["c"] = "\u0001é😀",
        });

        Assert.Equal(expected, await new FluidParser().Parse(source).RenderAsync(context));
    }
}
